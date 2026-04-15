# Assembly Download and ZIP Packaging Flow

This document describes the safest backend-only packaging strategy when:

- the physical files are stored in Google Drive
- metadata and BOM live in a relational database
- SolidWorks itself is not installed on the server
- the server can still use SolidWorks Document Manager API

## Why this is hard

SolidWorks assemblies usually store fully qualified reference paths.
If you simply:

1. query the assembly tree
2. download all files
3. zip the original folder tree

then the end user can still hit "missing reference" errors after extraction,
because the references inside the assembly may still point to old absolute paths.

## Recommended packaging strategy

Use a backend-generated "Pack and Go style" staging folder:

1. query the full dependency closure from the BOM table
2. download every required file into a temporary staging folder
3. flatten the package into one folder, or use a deterministic package path
4. detect duplicate file names and rename collisions
5. rewrite references inside copied assembly and subassembly files
6. save the copied files
7. create a ZIP from the staging folder

The important part is step 5:

- for parts and assemblies, call Document Manager external-reference APIs first
- then call `ReplaceReference(...)`
- then call `Save()`

This is the closest backend-only equivalent to SolidWorks Pack and Go.

## Dependency query

Use a recursive CTE on `pdm_bom_occurrences` so the root assembly download
includes all nested subassemblies and parts.

```sql
WITH RECURSIVE bom_tree AS (
    SELECT
        b.parent_version_id,
        b.child_version_id,
        b.occurrence_path,
        1 AS depth,
        ARRAY[b.parent_version_id, COALESCE(b.child_version_id, -1)] AS visited_chain
    FROM pdm_bom_occurrences b
    WHERE b.parent_version_id = @rootVersionId
      AND b.reference_status = 'Resolved'
      AND b.is_suppressed = FALSE

    UNION ALL

    SELECT
        b.parent_version_id,
        b.child_version_id,
        b.occurrence_path,
        bt.depth + 1,
        bt.visited_chain || COALESCE(b.child_version_id, -1)
    FROM pdm_bom_occurrences b
    INNER JOIN bom_tree bt
        ON b.parent_version_id = bt.child_version_id
    WHERE b.reference_status = 'Resolved'
      AND b.is_suppressed = FALSE
      AND NOT COALESCE(b.child_version_id, -1) = ANY(bt.visited_chain)
)
SELECT DISTINCT
    v.version_id,
    d.document_type,
    v.google_drive_file_id,
    v.original_file_name,
    v.source_file_path,
    v.vault_relative_path
FROM (
    SELECT @rootVersionId AS version_id
    UNION
    SELECT child_version_id
    FROM bom_tree
    WHERE child_version_id IS NOT NULL
) needed
INNER JOIN pdm_document_versions v
    ON v.version_id = needed.version_id
INNER JOIN pdm_documents d
    ON d.document_id = v.document_id;
```

## C# service flow

```csharp
public async Task<string> BuildAssemblyZipAsync(
    long rootVersionId,
    string tempRoot,
    CancellationToken cancellationToken = default)
{
    // 1. Query the root assembly and all descendants from PostgreSQL.
    IReadOnlyList<PdmPackageFile> files = await _repository
        .GetPackageClosureAsync(rootVersionId, cancellationToken);

    if (files.Count == 0)
    {
        throw new InvalidOperationException("No files were returned for the assembly package.");
    }

    // 2. Create a per-request staging folder.
    string packageId = Guid.NewGuid().ToString("N");
    string packageRoot = Path.Combine(tempRoot, packageId);
    string workingFolder = Path.Combine(packageRoot, "package");
    Directory.CreateDirectory(workingFolder);

    // 3. Build a collision-safe package file name map.
    //    Example output:
    //    TopAsm.sldasm
    //    Bracket.sldprt
    //    Bracket__2.sldprt
    Dictionary<long, string> packageFileMap = BuildUniquePackageNames(files);

    // 4. Download every file from Google Drive to the staging folder.
    foreach (PdmPackageFile file in files)
    {
        string stagedPath = Path.Combine(workingFolder, packageFileMap[file.VersionId]);
        file.StagedAbsolutePath = stagedPath;

        await _driveStorage.DownloadFileAsync(
            file.GoogleDriveFileId,
            stagedPath,
            cancellationToken);
    }

    // 5. Rewrite references inside copied assemblies and drawings.
    //    Process children first, then parents.
    foreach (PdmPackageFile parent in files
        .Where(f => f.DocumentType is "Assembly" or "Drawing")
        .OrderByDescending(f => f.Depth))
    {
        var dependencyRows = await _repository
            .GetImmediateChildrenAsync(parent.VersionId, cancellationToken);

        RewriteReferencesInCopiedFile(
            parent.StagedAbsolutePath,
            parent.DocumentType,
            dependencyRows,
            files);
    }

    // 6. Zip the staging folder.
    string zipPath = Path.Combine(packageRoot, $"{packageFileMap[rootVersionId]}.zip");
    ZipFile.CreateFromDirectory(workingFolder, zipPath, CompressionLevel.Fastest, includeBaseDirectory: false);

    return zipPath;
}
```

## Reference rewrite logic

The database should store:

- `source_reference_path`: the exact path returned by Document Manager when the parent was parsed
- `package_relative_path`: the target path in the generated package

That lets the backend rewrite references deterministically.

```csharp
private void RewriteReferencesInCopiedFile(
    string copiedParentFilePath,
    string documentType,
    IReadOnlyList<BomLink> childLinks,
    IReadOnlyList<PdmPackageFile> allFiles)
{
    SwDmDocumentType swDocType = documentType switch
    {
        "Assembly" => SwDmDocumentType.swDmDocumentAssembly,
        "Drawing" => SwDmDocumentType.swDmDocumentDrawing,
        _ => throw new NotSupportedException(documentType)
    };

    SwDMDocument18 doc = (SwDMDocument18)_dmApp.GetDocument(
        copiedParentFilePath,
        swDocType,
        allowReadOnly: false,
        out SwDmDocumentOpenError openError);

    if (doc is null || openError != SwDmDocumentOpenError.swDmDocumentOpenErrorNone)
    {
        throw new InvalidOperationException($"Failed to open '{copiedParentFilePath}' for rewrite. {openError}");
    }

    try
    {
        // For assemblies, use GetExternalFeatureReferences3 before ReplaceReference.
        // For drawings, use GetAllExternalReferences4 before ReplaceReference.
        PrimeReferenceCache(doc, swDocType, copiedParentFilePath);

        foreach (BomLink child in childLinks.Where(x => x.ChildVersionId.HasValue))
        {
            PdmPackageFile targetFile = allFiles.Single(x => x.VersionId == child.ChildVersionId!.Value);

            // Replace the original reference path with the copied package file path.
            doc.ReplaceReference(child.SourceReferencePath, targetFile.StagedAbsolutePath);
        }

        SwDmDocumentSaveError saveError = doc.Save();
        if (saveError != SwDmDocumentSaveError.swDmDocumentSaveErrorNone)
        {
            throw new InvalidOperationException($"Save failed for '{copiedParentFilePath}'. {saveError}");
        }
    }
    finally
    {
        doc.CloseDoc();
    }
}
```

## Important implementation note

The most reliable ZIP structure is usually a flat package folder:

- put the root assembly, subassemblies, and parts into the same folder
- rewrite collisions to unique names
- update references to the renamed copies before zipping

Why flattening is safer:

- it avoids subfolder search-path problems after extraction
- it behaves more like Pack and Go
- it removes ambiguity when two different children share the same original file name

If you must preserve folders, store `package_relative_path` in the database and
rewrite to the copied files in that structure. However, flattening is normally
the lower-risk choice for download packages.

## Supporting record shapes

```csharp
public sealed class PdmPackageFile
{
    public long VersionId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string GoogleDriveFileId { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string SourceFilePath { get; set; } = string.Empty;
    public string VaultRelativePath { get; set; } = string.Empty;
    public string StagedAbsolutePath { get; set; } = string.Empty;
    public int Depth { get; set; }
}

public sealed class BomLink
{
    public long ParentVersionId { get; set; }
    public long? ChildVersionId { get; set; }
    public string SourceReferencePath { get; set; } = string.Empty;
    public string PackageRelativePath { get; set; } = string.Empty;
}
```

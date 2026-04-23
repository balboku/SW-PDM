namespace SWPdm.Api.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SWPdm.Api.Configuration;
using SWPdm.Api.Contracts;
using SWPdm.Sample.Data;
using SWPdm.Sample.Data.Entities;
using SWPdm.Sample.Services;
using System.Security.Cryptography;

public sealed class PdmIngestionService
{
    private readonly PdmDbContext _dbContext;
    private readonly IOptions<LocalStorageOptions> _localStorageOptions;
    private readonly IOptions<SolidWorksDocumentManagerOptions> _solidWorksOptions;
    private readonly LocalStorageService _localStorageService;
    private readonly SolidWorksDocumentManagerServiceFactory _solidWorksDocumentManagerServiceFactory;
    private readonly ILogger<PdmIngestionService> _logger;

    public PdmIngestionService(
        PdmDbContext dbContext,
        IOptions<LocalStorageOptions> localStorageOptions,
        IOptions<SolidWorksDocumentManagerOptions> solidWorksOptions,
        LocalStorageService localStorageService,
        SolidWorksDocumentManagerServiceFactory solidWorksDocumentManagerServiceFactory,
        ILogger<PdmIngestionService> logger)
    {
        _dbContext = dbContext;
        _localStorageOptions = localStorageOptions;
        _solidWorksOptions = solidWorksOptions;
        _localStorageService = localStorageService;
        _solidWorksDocumentManagerServiceFactory = solidWorksDocumentManagerServiceFactory;
        _logger = logger;
    }

    public async Task<IngestCadFileResponse> IngestAsync(
        IngestCadFileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.LocalFilePath))
        {
            throw new ArgumentException("LocalFilePath is required.", nameof(request));
        }

        string rootFilePath = Path.GetFullPath(request.LocalFilePath);
        if (!File.Exists(rootFilePath))
        {
            throw new FileNotFoundException("The SolidWorks file to ingest was not found.", rootFilePath);
        }

        string[] baseSearchPaths = _solidWorksOptions.Value.ReferenceSearchPaths
            .Concat(request.AdditionalSearchPaths ?? Array.Empty<string>())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        using SolidWorksDocumentManagerService documentManager = _solidWorksDocumentManagerServiceFactory.Create();

        List<string> issues = new();
        Dictionary<string, IngestedCadNode> cache = new(StringComparer.OrdinalIgnoreCase);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        IngestedCadNode root = await IngestFileInternalAsync(
            rootFilePath,
            request.IngestReferencedFiles,
            baseSearchPaths,
            documentManager,
            cache,
            issues,
            true, // isRoot
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        IReadOnlyList<IngestedFileResponse> files = cache.Values
            .OrderBy(x => x.SourceFilePath, StringComparer.OrdinalIgnoreCase)
            .Select(x => new IngestedFileResponse(
                SourceFilePath: x.SourceFilePath,
                DocumentId: x.DocumentId,
                VersionId: x.VersionId,
                DocumentType: x.DocumentType,
                PartNumber: x.PartNumber,
                StorageFileId: x.StorageFileId,
                CreatedDocument: x.CreatedDocument,
                VersionNo: x.VersionNo))
            .ToArray();

        return new IngestCadFileResponse(
            RootDocumentId: root.DocumentId,
            RootVersionId: root.VersionId,
            RootDocumentType: root.DocumentType,
            RootStorageFileId: root.StorageFileId,
            ProcessedFileCount: files.Count,
            Files: files,
            Issues: issues);
    }

    private async Task<IngestedCadNode> IngestFileInternalAsync(
        string filePath,
        bool ingestReferencedFiles,
        IReadOnlyCollection<string> inheritedSearchPaths,
        SolidWorksDocumentManagerService documentManager,
        IDictionary<string, IngestedCadNode> cache,
        ICollection<string> issues,
        bool isRoot,
        CancellationToken cancellationToken)
    {
        string normalizedPath = Path.GetFullPath(filePath);

        if (cache.TryGetValue(normalizedPath, out IngestedCadNode? cached))
        {
            return cached;
        }

        List<string> effectiveSearchPaths = inheritedSearchPaths.ToList();
        string? folder = Path.GetDirectoryName(normalizedPath);
        if (!string.IsNullOrWhiteSpace(folder) && !effectiveSearchPaths.Contains(folder, StringComparer.OrdinalIgnoreCase))
        {
            effectiveSearchPaths.Add(folder);
        }

        SolidWorksParseResult parseResult = documentManager.Parse(normalizedPath, effectiveSearchPaths);
        string documentType = MapDocumentType(parseResult.DocumentType);
        string? partNumber = ExtractProperty(parseResult, "PartNumber", "Number", "Part No", "PartNo", "品號");

        // 強制驗證：PartNumber / 品號 必須存在於 CAD 檔案的自訂屬性中
        if (string.IsNullOrWhiteSpace(partNumber))
        {
            throw new InvalidOperationException(
                $"解析失敗：CAD 檔案內部未設定 PartNumber 或 品號 屬性，請在 SolidWorks 填寫後再上傳。(檔案：{Path.GetFileName(normalizedPath)})");
        }

        // 防呆檢查：建立新文件時，確保 PartNumber 尚未被使用
        bool isNewDocument = (await FindDocumentForIngestAsync(documentType, partNumber, normalizedPath, cancellationToken)) is null;
        if (isRoot && isNewDocument)
        {
            bool partNumberAlreadyExists = await _dbContext.Documents
                .AnyAsync(d => d.PartNumber == partNumber, cancellationToken);
            if (partNumberAlreadyExists)
            {
                throw new InvalidOperationException(
                    $"入庫失敗：系統中已存在料號為 {partNumber} 的圖檔，無法重複建立。");
            }
        }

        string? material = ExtractProperty(parseResult, "Material");
        string? designer = ExtractProperty(parseResult, "Designer", "DesignedBy", "Author");
        string? revision = ExtractProperty(parseResult, "Revision", "Rev");

        Dictionary<string, IngestedCadNode?> childNodesByPath = new(StringComparer.OrdinalIgnoreCase);

        if (ingestReferencedFiles && parseResult.DocumentType == SolidWorksDocumentKind.Assembly)
        {
            foreach (string referencedPath in parseResult.ReferencedFilePaths)
            {
                string childPath = Path.GetFullPath(referencedPath);

                try
                {
                    if (!File.Exists(childPath))
                    {
                        issues.Add($"Referenced file was not found during ingest: {childPath}");
                        childNodesByPath[childPath] = null;
                        continue;
                    }

                    IngestedCadNode childNode = await IngestFileInternalAsync(
                        childPath,
                        ingestReferencedFiles,
                        effectiveSearchPaths,
                        documentManager,
                        cache,
                        issues,
                        false, // not root
                        cancellationToken);

                    childNodesByPath[childPath] = childNode;
                }
                catch (Exception ex) when (ex is FileNotFoundException or NotSupportedException or InvalidOperationException)
                {
                    issues.Add($"Referenced file ingest failed for '{childPath}': {ex.Message}");
                    childNodesByPath[childPath] = null;
                }
            }
        }

        PdmDocument? existingDocument = await FindDocumentForIngestAsync(documentType, partNumber, normalizedPath, cancellationToken);

        if (existingDocument is null)
        {
            // Will create a new one
        }
        else if (!string.IsNullOrWhiteSpace(existingDocument.CheckedOutBy))
        {
            throw new InvalidOperationException($"Document '{existingDocument.FileName}' cannot be updated because it is checked out by {existingDocument.CheckedOutBy}.");
        }

        bool createdDocument = existingDocument is null;

        PdmDocument document = existingDocument ?? new PdmDocument
        {
            FileName = Path.GetFileNameWithoutExtension(normalizedPath),
            FileExtension = Path.GetExtension(normalizedPath),
            DocumentType = documentType,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        document.FileName = Path.GetFileNameWithoutExtension(normalizedPath);
        document.FileExtension = Path.GetExtension(normalizedPath);
        document.DocumentType = documentType;
        document.PartNumber = partNumber;
        document.RevisionLabel = revision;
        document.Material = material;
        document.Designer = designer;
        document.IsActive = true;
        document.UpdatedAt = DateTimeOffset.UtcNow;

        if (createdDocument)
        {
            _dbContext.Documents.Add(document);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        int nextVersionNo = await _dbContext.DocumentVersions
            .Where(x => x.DocumentId == document.DocumentId)
            .Select(x => (int?)x.VersionNo)
            .MaxAsync(cancellationToken) + 1 ?? 1;

        string checksumSha256;
        using (var stream = File.OpenRead(normalizedPath))
        using (var sha256 = SHA256.Create())
        {
            byte[] hash = await sha256.ComputeHashAsync(stream, cancellationToken);
            checksumSha256 = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        string storageFileId;
        var existingVersionStorageId = await _dbContext.DocumentVersions
            .Where(x => x.ChecksumSha256 == checksumSha256)
            .Select(x => x.StorageFileId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(existingVersionStorageId))
        {
            storageFileId = existingVersionStorageId;
            _logger.LogInformation("File dedup matched for {Path}, reusing storage file {StorageFileId}", normalizedPath, storageFileId);
        }
        else
        {
            storageFileId = await _localStorageService.UploadFileAsync(normalizedPath, documentType, cancellationToken);
        }

        PdmDocumentVersion version = new()
        {
            DocumentId = document.DocumentId,
            VersionNo = nextVersionNo,
            RevisionLabel = revision,
            StorageFileId = storageFileId,
            OriginalFileName = Path.GetFileName(normalizedPath),
            SourceFilePath = normalizedPath,
            VaultRelativePath = BuildVaultRelativePath(documentType, normalizedPath, partNumber),
            ChecksumSha256 = checksumSha256,
            FileSizeBytes = new FileInfo(normalizedPath).Length,
            SourceLastWriteUtc = File.GetLastWriteTimeUtc(normalizedPath),
            ParsedAt = DateTimeOffset.UtcNow,
            LifecycleState = "WIP",
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.DocumentVersions.Add(version);
        await _dbContext.SaveChangesAsync(cancellationToken);

        document.CurrentVersionId = version.VersionId;
        _dbContext.Documents.Update(document);

        await ReplaceCustomPropertiesAsync(version.VersionId, parseResult, cancellationToken);
        await ReplaceBomRowsAsync(version.VersionId, parseResult, childNodesByPath, issues, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        IngestedCadNode result = new(
            SourceFilePath: normalizedPath,
            DocumentId: document.DocumentId,
            VersionId: version.VersionId,
            DocumentType: documentType,
            PartNumber: partNumber,
            StorageFileId: storageFileId,
            CreatedDocument: createdDocument,
            VersionNo: nextVersionNo);

        cache[normalizedPath] = result;

        _logger.LogInformation(
            "Ingested CAD file {SourceFilePath} as DocumentId={DocumentId}, VersionId={VersionId}",
            normalizedPath,
            document.DocumentId,
            version.VersionId);

        return result;
    }

    private async Task<PdmDocument?> FindDocumentForIngestAsync(
        string documentType,
        string? partNumber,
        string sourceFilePath,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(partNumber))
        {
            PdmDocument? byPartNumber = await _dbContext.Documents
                .SingleOrDefaultAsync(
                    x => x.DocumentType == documentType && x.PartNumber == partNumber,
                    cancellationToken);

            if (byPartNumber is not null)
            {
                return byPartNumber;
            }
        }

        return await _dbContext.DocumentVersions
            .Where(x => x.SourceFilePath == sourceFilePath && x.Document.DocumentType == documentType)
            .OrderByDescending(x => x.VersionNo)
            .Select(x => x.Document)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task ReplaceCustomPropertiesAsync(
        long versionId,
        SolidWorksParseResult parseResult,
        CancellationToken cancellationToken)
    {
        List<PdmCustomProperty> properties = new();

        foreach ((string propertyName, SolidWorksCustomProperty property) in parseResult.DocumentProperties)
        {
            properties.Add(new PdmCustomProperty
            {
                VersionId = versionId,
                ConfigurationName = string.Empty,
                PropertyName = propertyName,
                PropertyValue = property.Value,
                PropertyType = property.PropertyType,
                RawExpression = null,
                IsResolved = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        foreach ((string configurationName, IReadOnlyDictionary<string, SolidWorksCustomProperty> configurationProperties) in parseResult.ConfigurationProperties)
        {
            foreach ((string propertyName, SolidWorksCustomProperty property) in configurationProperties)
            {
                properties.Add(new PdmCustomProperty
                {
                    VersionId = versionId,
                    ConfigurationName = configurationName,
                    PropertyName = propertyName,
                    PropertyValue = property.Value,
                    PropertyType = property.PropertyType,
                    RawExpression = null,
                    IsResolved = true,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        if (properties.Count > 0)
        {
            _dbContext.CustomProperties.AddRange(properties);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ReplaceBomRowsAsync(
        long parentVersionId,
        SolidWorksParseResult parseResult,
        IReadOnlyDictionary<string, IngestedCadNode?> childNodesByPath,
        ICollection<string> issues,
        CancellationToken cancellationToken)
    {
        List<PdmBomOccurrence> bomRows = new();

        if (parseResult.DocumentType == SolidWorksDocumentKind.Assembly)
        {
            for (int index = 0; index < parseResult.ReferencedFilePaths.Count; index++)
            {
                string referencePath = Path.GetFullPath(parseResult.ReferencedFilePaths[index]);
                childNodesByPath.TryGetValue(referencePath, out IngestedCadNode? childNode);

                if (childNode is null && File.Exists(referencePath))
                {
                    PdmDocumentVersion? existingChildVersion = await _dbContext.DocumentVersions
                        .Where(x => x.SourceFilePath == referencePath)
                        .OrderByDescending(x => x.VersionNo)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (existingChildVersion is not null)
                    {
                        childNode = new IngestedCadNode(
                            SourceFilePath: referencePath,
                            DocumentId: existingChildVersion.DocumentId,
                            VersionId: existingChildVersion.VersionId,
                            DocumentType: string.Empty,
                            PartNumber: null,
                            StorageFileId: existingChildVersion.StorageFileId,
                            CreatedDocument: false,
                            VersionNo: existingChildVersion.VersionNo);
                    }
                }

                string referenceStatus = childNode is not null ? "Resolved" : "Missing";

                if (childNode is null)
                {
                    issues.Add($"BOM reference was recorded as missing: {referencePath}");
                }

                bomRows.Add(new PdmBomOccurrence
                {
                    ParentVersionId = parentVersionId,
                    ChildVersionId = childNode?.VersionId,
                    OccurrencePath = $"{index + 1}:{referencePath}",
                    ParentConfigurationName = string.Empty,
                    ChildConfigurationName = string.Empty,
                    Quantity = 1m,
                    FindNumber = null,
                    SourceReferencePath = referencePath,
                    PackageRelativePath = Path.GetFileName(referencePath),
                    ReferenceStatus = referenceStatus,
                    IsSuppressed = false,
                    IsVirtual = false,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        if (bomRows.Count > 0)
        {
            _dbContext.BomOccurrences.AddRange(bomRows);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static string MapDocumentType(SolidWorksDocumentKind documentKind)
    {
        return documentKind switch
        {
            SolidWorksDocumentKind.Part => "Part",
            SolidWorksDocumentKind.Assembly => "Assembly",
            SolidWorksDocumentKind.Drawing => "Drawing",
            _ => throw new NotSupportedException($"Unsupported SolidWorks document kind: {documentKind}.")
        };
    }

    private static string BuildVaultRelativePath(string documentType, string sourceFilePath, string? partNumber)
    {
        string fileName = Path.GetFileName(sourceFilePath);
        string safePartNumber = string.IsNullOrWhiteSpace(partNumber)
            ? "_unclassified"
            : SanitizePathSegment(partNumber);

        return Path.Combine(documentType, safePartNumber, fileName).Replace('\\', '/');
    }

    private static string SanitizePathSegment(string value)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        char[] buffer = value
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray();

        return new string(buffer);
    }

    private static string? ExtractProperty(
        SolidWorksParseResult parseResult,
        params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (parseResult.DocumentProperties.TryGetValue(propertyName, out SolidWorksCustomProperty? documentProperty)
                && !string.IsNullOrWhiteSpace(documentProperty.Value))
            {
                return documentProperty.Value;
            }

            foreach (IReadOnlyDictionary<string, SolidWorksCustomProperty> configurationProperties in parseResult.ConfigurationProperties.Values)
            {
                if (configurationProperties.TryGetValue(propertyName, out SolidWorksCustomProperty? configurationProperty)
                    && !string.IsNullOrWhiteSpace(configurationProperty.Value))
                {
                    return configurationProperty.Value;
                }
            }
        }

        return null;
    }

    private sealed record IngestedCadNode(
        string SourceFilePath,
        long DocumentId,
        long VersionId,
        string DocumentType,
        string? PartNumber,
        string StorageFileId,
        bool CreatedDocument,
        int VersionNo);
}

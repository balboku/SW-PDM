namespace SWPdm.Sample.Data.Repositories;

using System.Data;
using Microsoft.EntityFrameworkCore;
using SWPdm.Sample.Data.Entities;
using SWPdm.Sample.Data.Models;

public sealed class PdmRepository : IPdmRepository
{
    private readonly PdmDbContext _dbContext;

    public PdmRepository(PdmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.Database.CanConnectAsync(cancellationToken);
    }

    public async Task<PdmDocumentDetails?> GetDocumentAsync(
        long documentId,
        CancellationToken cancellationToken = default)
    {
        PdmDocument? document = await _dbContext.Documents
            .AsNoTracking()
            .Include(x => x.Versions)
            .SingleOrDefaultAsync(x => x.DocumentId == documentId, cancellationToken);

        if (document is null)
        {
            return null;
        }

        IReadOnlyList<PdmDocumentVersionSummary> versions = document.Versions
            .OrderByDescending(x => x.VersionNo)
            .Select(x => new PdmDocumentVersionSummary(
                x.VersionId,
                x.VersionNo,
                x.RevisionLabel,
                x.OriginalFileName,
                x.StorageFileId,
                x.CreatedAt))
            .ToArray();

        return new PdmDocumentDetails(
            document.DocumentId,
            document.FileName,
            document.FileExtension,
            document.DocumentType,
            document.PartNumber,
            document.RevisionLabel,
            document.Material,
            document.Designer,
            document.CurrentVersionId,
            document.IsActive,
            document.CreatedAt,
            document.UpdatedAt,
            versions);
    }

    public async Task<PdmVersionDetails?> GetVersionAsync(
        long versionId,
        CancellationToken cancellationToken = default)
    {
        PdmDocumentVersion? version = await _dbContext.DocumentVersions
            .AsNoTracking()
            .Include(x => x.CustomProperties)
            .SingleOrDefaultAsync(x => x.VersionId == versionId, cancellationToken);

        if (version is null)
        {
            return null;
        }

        IReadOnlyList<PdmCustomPropertyData> customProperties = version.CustomProperties
            .OrderBy(x => x.ConfigurationName)
            .ThenBy(x => x.PropertyName)
            .Select(x => new PdmCustomPropertyData(
                x.CustomPropertyId,
                x.ConfigurationName,
                x.PropertyName,
                x.PropertyValue,
                x.PropertyType,
                x.RawExpression,
                x.IsResolved))
            .ToArray();

        IReadOnlyList<PdmBomLinkData> immediateChildren = await GetImmediateChildrenAsync(versionId, cancellationToken);

        return new PdmVersionDetails(
            version.VersionId,
            version.DocumentId,
            version.VersionNo,
            version.RevisionLabel,
            version.StorageFileId,
            version.OriginalFileName,
            version.SourceFilePath,
            version.VaultRelativePath,
            version.ChecksumSha256,
            version.FileSizeBytes,
            version.SourceLastWriteUtc,
            version.ParsedAt,
            version.CreatedAt,
            customProperties,
            immediateChildren);
    }

    public async Task<IReadOnlyList<PdmBomLinkData>> GetImmediateChildrenAsync(
        long parentVersionId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.BomOccurrences
            .AsNoTracking()
            .Where(x => x.ParentVersionId == parentVersionId)
            .OrderBy(x => x.OccurrencePath)
            .Select(x => new PdmBomLinkData(
                x.BomOccurrenceId,
                x.ParentVersionId,
                x.ChildVersionId,
                x.OccurrencePath,
                x.ParentConfigurationName,
                x.ChildConfigurationName,
                x.Quantity,
                x.FindNumber,
                x.SourceReferencePath,
                x.PackageRelativePath,
                x.ReferenceStatus,
                x.IsSuppressed,
                x.IsVirtual,
                x.ChildVersion != null ? x.ChildVersion.Document.DocumentType : null,
                x.ChildVersion != null ? x.ChildVersion.OriginalFileName : null,
                x.ChildVersion != null ? x.ChildVersion.StorageFileId : null))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PdmPackageFile>> GetPackageClosureAsync(
        long rootVersionId,
        CancellationToken cancellationToken = default)
    {
        string sql = ResolvePackageClosureSql();

        await using var connection = _dbContext.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@rootVersionId";
        parameter.Value = rootVersionId;
        command.Parameters.Add(parameter);

        List<PdmPackageFile> result = new();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new PdmPackageFile(
                VersionId: reader.GetInt64(0),
                DocumentType: reader.GetString(1),
                StorageFileId: reader.GetString(2),
                OriginalFileName: reader.GetString(3),
                SourceFilePath: reader.GetString(4),
                VaultRelativePath: reader.GetString(5),
                Depth: reader.GetInt32(6)));
        }

        return result;
    }

    public async Task<IReadOnlyList<PdmPackageFile>> GetWhereUsedAsync(
        long childVersionId,
        CancellationToken cancellationToken = default)
    {
        string sql = ResolveWhereUsedSql();

        await using var connection = _dbContext.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@childVersionId";
        parameter.Value = childVersionId;
        command.Parameters.Add(parameter);

        List<PdmPackageFile> result = new();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new PdmPackageFile(
                VersionId: reader.GetInt64(0),
                DocumentType: reader.GetString(1),
                StorageFileId: reader.GetString(2),
                OriginalFileName: reader.GetString(3),
                SourceFilePath: reader.GetString(4),
                VaultRelativePath: reader.GetString(5),
                Depth: reader.GetInt32(6)));
        }

        return result;
    }

    private string ResolvePackageClosureSql()
    {
        string? provider = _dbContext.Database.ProviderName;

        if (provider?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true)
        {
            return
                """
                WITH bom_tree AS (
                    SELECT
                        b.parent_version_id,
                        b.child_version_id,
                        1 AS depth,
                        CAST(CONCAT(',', b.parent_version_id, ',', COALESCE(CAST(b.child_version_id AS varchar(30)), '-1'), ',') AS varchar(max)) AS visited_chain
                    FROM pdm_bom_occurrences b
                    WHERE b.parent_version_id = @rootVersionId
                      AND b.reference_status = 'Resolved'
                      AND b.is_suppressed = 0

                    UNION ALL

                    SELECT
                        b.parent_version_id,
                        b.child_version_id,
                        bt.depth + 1 AS depth,
                        CONCAT(bt.visited_chain, COALESCE(CAST(b.child_version_id AS varchar(30)), '-1'), ',') AS visited_chain
                    FROM pdm_bom_occurrences b
                    INNER JOIN bom_tree bt
                        ON b.parent_version_id = bt.child_version_id
                    WHERE b.reference_status = 'Resolved'
                      AND b.is_suppressed = 0
                      AND CHARINDEX(CONCAT(',', COALESCE(CAST(b.child_version_id AS varchar(30)), '-1'), ','), bt.visited_chain) = 0
                ),
                needed_versions AS (
                    SELECT CAST(@rootVersionId AS bigint) AS version_id, 0 AS depth
                    UNION ALL
                    SELECT child_version_id AS version_id, depth
                    FROM bom_tree
                    WHERE child_version_id IS NOT NULL
                ),
                min_depths AS (
                    SELECT
                        version_id,
                        MIN(depth) AS depth
                    FROM needed_versions
                    GROUP BY version_id
                )
                SELECT
                    md.version_id,
                    d.document_type,
                    v.storage_file_id,
                    v.original_file_name,
                    v.source_file_path,
                    v.vault_relative_path,
                    md.depth
                FROM min_depths md
                INNER JOIN pdm_document_versions v
                    ON v.version_id = md.version_id
                INNER JOIN pdm_documents d
                    ON d.document_id = v.document_id
                ORDER BY md.depth, md.version_id;
                """;
        }

        return
            """
            WITH RECURSIVE bom_tree AS (
                SELECT
                    b.parent_version_id,
                    b.child_version_id,
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
                    bt.depth + 1 AS depth,
                    bt.visited_chain || COALESCE(b.child_version_id, -1) AS visited_chain
                FROM pdm_bom_occurrences b
                INNER JOIN bom_tree bt
                    ON b.parent_version_id = bt.child_version_id
                WHERE b.reference_status = 'Resolved'
                  AND b.is_suppressed = FALSE
                  AND NOT COALESCE(b.child_version_id, -1) = ANY(bt.visited_chain)
            ),
            needed_versions AS (
                SELECT @rootVersionId::bigint AS version_id, 0 AS depth
                UNION ALL
                SELECT child_version_id AS version_id, depth
                FROM bom_tree
                WHERE child_version_id IS NOT NULL
            ),
            min_depths AS (
                SELECT
                    version_id,
                    MIN(depth) AS depth
                FROM needed_versions
                GROUP BY version_id
            )
            SELECT
                md.version_id,
                d.document_type,
                v.storage_file_id,
                v.original_file_name,
                v.source_file_path,
                v.vault_relative_path,
                md.depth
            FROM min_depths md
            INNER JOIN pdm_document_versions v
                ON v.version_id = md.version_id
            INNER JOIN pdm_documents d
                ON d.document_id = v.document_id
            """;
    }

    private string ResolveWhereUsedSql()
    {
        string? provider = _dbContext.Database.ProviderName;

        if (provider?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true)
        {
            return
                """
                WITH bom_tree AS (
                    SELECT
                        b.parent_version_id,
                        b.child_version_id,
                        1 AS depth,
                        CAST(CONCAT(',', COALESCE(CAST(b.parent_version_id AS varchar(30)), '-1'), ',', CAST(b.child_version_id AS varchar(30)), ',') AS varchar(max)) AS visited_chain
                    FROM pdm_bom_occurrences b
                    WHERE b.child_version_id = @childVersionId
                      AND b.reference_status = 'Resolved'
                      AND b.is_suppressed = 0

                    UNION ALL

                    SELECT
                        b.parent_version_id,
                        b.child_version_id,
                        bt.depth + 1 AS depth,
                        CONCAT(bt.visited_chain, COALESCE(CAST(b.parent_version_id AS varchar(30)), '-1'), ',') AS visited_chain
                    FROM pdm_bom_occurrences b
                    INNER JOIN bom_tree bt
                        ON b.child_version_id = bt.parent_version_id
                    WHERE b.reference_status = 'Resolved'
                      AND b.is_suppressed = 0
                      AND CHARINDEX(CONCAT(',', COALESCE(CAST(b.parent_version_id AS varchar(30)), '-1'), ','), bt.visited_chain) = 0
                ),
                needed_versions AS (
                    SELECT parent_version_id AS version_id, depth
                    FROM bom_tree
                    WHERE parent_version_id IS NOT NULL
                ),
                min_depths AS (
                    SELECT
                        version_id,
                        MIN(depth) AS depth
                    FROM needed_versions
                    GROUP BY version_id
                )
                SELECT
                    md.version_id,
                    d.document_type,
                    v.storage_file_id,
                    v.original_file_name,
                    v.source_file_path,
                    v.vault_relative_path,
                    md.depth
                FROM min_depths md
                INNER JOIN pdm_document_versions v
                    ON v.version_id = md.version_id
                INNER JOIN pdm_documents d
                    ON d.document_id = v.document_id
                ORDER BY md.depth, md.version_id;
                """;
        }

        return
            """
            WITH RECURSIVE bom_tree AS (
                SELECT
                    b.parent_version_id,
                    b.child_version_id,
                    1 AS depth,
                    ARRAY[COALESCE(b.parent_version_id, -1), b.child_version_id] AS visited_chain
                FROM pdm_bom_occurrences b
                WHERE b.child_version_id = @childVersionId
                  AND b.reference_status = 'Resolved'
                  AND b.is_suppressed = FALSE

                UNION ALL

                SELECT
                    b.parent_version_id,
                    b.child_version_id,
                    bt.depth + 1 AS depth,
                    bt.visited_chain || COALESCE(b.parent_version_id, -1) AS visited_chain
                FROM pdm_bom_occurrences b
                INNER JOIN bom_tree bt
                    ON b.child_version_id = bt.parent_version_id
                WHERE b.reference_status = 'Resolved'
                  AND b.is_suppressed = FALSE
                  AND NOT COALESCE(b.parent_version_id, -1) = ANY(bt.visited_chain)
            ),
            needed_versions AS (
                SELECT parent_version_id AS version_id, depth
                FROM bom_tree
                WHERE parent_version_id IS NOT NULL
            ),
            min_depths AS (
                SELECT
                    version_id,
                    MIN(depth) AS depth
                FROM needed_versions
                GROUP BY version_id
            )
            SELECT
                md.version_id,
                d.document_type,
                v.storage_file_id,
                v.original_file_name,
                v.source_file_path,
                v.vault_relative_path,
                md.depth
            FROM min_depths md
            INNER JOIN pdm_document_versions v
                ON v.version_id = md.version_id
            INNER JOIN pdm_documents d
                ON d.document_id = v.document_id
            ORDER BY md.depth, md.version_id;
            """;
    }
}

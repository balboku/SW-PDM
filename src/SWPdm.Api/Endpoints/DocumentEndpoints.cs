using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SWPdm.Api.Configuration;
using SWPdm.Api.Contracts;
using SWPdm.Api.Services;
using SWPdm.Sample.Data;
using SWPdm.Sample.Data.Models;
using SWPdm.Sample.Data.Repositories;
using SWPdm.Sample.Data.Entities;
using SWPdm.Sample.Services;

namespace SWPdm.Api.Endpoints;

public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this WebApplication app)
    {
        // 階段 1：搜尋圖檔的 API
        app.MapGet("/api/documents/search", async (
            string? query,
            PdmDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var queryable = dbContext.Documents
                    .Include(d => d.CurrentVersion)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(query))
                {
                    string searchPattern = $"%{query}%";
                    queryable = queryable.Where(d => 
                        EF.Functions.ILike(d.FileName, searchPattern) || 
                        EF.Functions.ILike(d.PartNumber, searchPattern));
                }

                var results = await queryable
                    .OrderByDescending(d => d.UpdatedAt)
                    .Take(50)
                    .Select(d => new
                    {
                        d.DocumentId,
                        d.FileName,
                        d.PartNumber,
                        d.DocumentType,
                        d.RevisionLabel,
                        d.Material,
                        CurrentVersionNo = d.CurrentVersion != null ? d.CurrentVersion.VersionNo : (int?)null,
                        d.UpdatedAt
                    })
                    .ToListAsync(cancellationToken);

                return Results.Ok(results);
            }
            catch (Exception ex)
            {
                return EndpointHelpers.ToProblem(ex);
            }
        });

        app.MapGet("/api/documents/{documentId:long}", async (
            long documentId,
            IPdmRepository repository,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var document = await repository.GetDocumentAsync(documentId, cancellationToken);
                return document is null ? Results.NotFound() : Results.Ok(document);
            }
            catch (Exception ex)
            {
                return EndpointHelpers.ToProblem(ex);
            }
        });

        app.MapGet("/api/versions/{versionId:long}", async (
            long versionId,
            IPdmRepository repository,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var version = await repository.GetVersionAsync(versionId, cancellationToken);
                return version is null ? Results.NotFound() : Results.Ok(version);
            }
            catch (Exception ex)
            {
                return EndpointHelpers.ToProblem(ex);
            }
        });

        app.MapGet("/api/versions/{versionId:long}/children", async (
            long versionId,
            IPdmRepository repository,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var children = await repository.GetImmediateChildrenAsync(versionId, cancellationToken);
                return Results.Ok(children);
            }
            catch (Exception ex)
            {
                return EndpointHelpers.ToProblem(ex);
            }
        });

        app.MapGet("/api/assemblies/{rootVersionId:long}/package-closure", async (
            long rootVersionId,
            IPdmRepository repository,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var closure = await repository.GetPackageClosureAsync(rootVersionId, cancellationToken);
                return Results.Ok(closure);
            }
            catch (Exception ex)
            {
                return EndpointHelpers.ToProblem(ex);
            }
        });

        app.MapGet("/api/assemblies/{rootVersionId:long}/download-zip", async (
            long rootVersionId,
            IPdmRepository repository,
            LocalStorageService storageService,
            CancellationToken cancellationToken) =>
        {
            IReadOnlyList<PdmPackageFile> files =
                await repository.GetPackageClosureAsync(rootVersionId, cancellationToken);

            if (files.Count == 0)
            {
                return Results.NotFound(new
                {
                    title = "Assembly not found",
                    detail = $"No package files found for rootVersionId={rootVersionId}. " +
                             "Verify the version exists and has resolved BOM references."
                });
            }

            string sessionId = Guid.NewGuid().ToString("N")[..8];
            string tempDir = Path.Combine(
                Path.GetTempPath(),
                $"swpdm_zip_{rootVersionId}_{sessionId}");
            Directory.CreateDirectory(tempDir);

            string zipPath = Path.Combine(
                Path.GetTempPath(),
                $"assembly_{rootVersionId}_{sessionId}.zip");

            try
            {
                List<string> downloadIssues = new();

                foreach (PdmPackageFile file in files)
                {
                    string safeFileName = SanitizeFileName(file.OriginalFileName);
                    string destPath = Path.Combine(tempDir, safeFileName);

                    if (File.Exists(destPath))
                    {
                        continue;
                    }

                    try
                    {
                        await storageService.DownloadFileAsync(
                            file.StorageFileId,
                            destPath,
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        downloadIssues.Add(
                            $"Failed to copy '{file.OriginalFileName}' " +
                            $"(storageId={file.StorageFileId}): {ex.Message}");
                    }
                }

                using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    foreach (string filePath in Directory.EnumerateFiles(tempDir))
                    {
                        archive.CreateEntryFromFile(
                            filePath,
                            Path.GetFileName(filePath),
                            CompressionLevel.Fastest);
                    }
                }

                byte[] zipBytes = await File.ReadAllBytesAsync(zipPath, cancellationToken);
                string downloadFileName = $"assembly_{rootVersionId}.zip";

                return Results.File(
                    zipBytes,
                    contentType: "application/zip",
                    fileDownloadName: downloadFileName);
            }
            catch (Exception ex)
            {
                return EndpointHelpers.ToProblem(ex);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, recursive: true);

                    if (File.Exists(zipPath))
                        File.Delete(zipPath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        });

        app.MapPost("/api/solidworks/parse", (
            ParseSolidWorksFileRequest request,
            IOptions<SolidWorksDocumentManagerOptions> solidWorksOptions,
            SolidWorksDocumentManagerServiceFactory documentManagerFactory) =>
        {
            if (string.IsNullOrWhiteSpace(request.FilePath))
            {
                return EndpointHelpers.ValidationError(nameof(request.FilePath), "FilePath is required.");
            }

            try
            {
                string[] mergedSearchPaths = solidWorksOptions.Value.ReferenceSearchPaths
                    .Concat(request.AdditionalSearchPaths ?? Array.Empty<string>())
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                using SolidWorksDocumentManagerService documentManager = documentManagerFactory.Create();
                SolidWorksParseResult result = documentManager.Parse(request.FilePath, mergedSearchPaths);

                return Results.Ok(new SolidWorksParseResponse(
                    FilePath: result.FilePath,
                    DocumentType: result.DocumentType.ToString(),
                    DocumentProperties: result.DocumentProperties,
                    ConfigurationProperties: result.ConfigurationProperties,
                    ReferencedFilePaths: result.ReferencedFilePaths));
            }
            catch (Exception ex)
            {
                return EndpointHelpers.ToProblem(ex);
            }
        });

        // ==========================================
        // 任務一：Where-Used
        // ==========================================
        app.MapGet("/api/versions/{versionId:long}/where-used", async (
            long versionId,
            IPdmRepository repository,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var parents = await repository.GetWhereUsedAsync(versionId, cancellationToken);
                return Results.Ok(parents);
            }
            catch (Exception ex)
            {
                return EndpointHelpers.ToProblem(ex);
            }
        });

        // ==========================================
        // 任務二：Check-in / Check-out 機制
        // ==========================================
        app.MapPost("/api/documents/{documentId:long}/checkout", async (
            long documentId,
            CheckOutRequest request,
            PdmDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.CheckOutBy))
            {
                return EndpointHelpers.ValidationError(nameof(request.CheckOutBy), "CheckOutBy provider is required.");
            }

            var document = await dbContext.Documents.FindAsync(new object[] { documentId }, cancellationToken);
            if (document is null) return Results.NotFound();

            if (!string.IsNullOrWhiteSpace(document.CheckedOutBy))
            {
                return Results.BadRequest($"Document is already checked out by {document.CheckedOutBy}.");
            }

            document.CheckedOutBy = request.CheckOutBy;
            document.CheckedOutAt = DateTimeOffset.UtcNow;
            
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { message = "Checked out successfully", checkedOutBy = document.CheckedOutBy });
        });

        app.MapPost("/api/documents/{documentId:long}/undo-checkout", async (
            long documentId,
            PdmDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var document = await dbContext.Documents.FindAsync(new object[] { documentId }, cancellationToken);
            if (document is null) return Results.NotFound();

            document.CheckedOutBy = null;
            document.CheckedOutAt = null;
            
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { message = "Undo check-out successfully" });
        });

        app.MapPost("/api/documents/{documentId:long}/checkin", async (
            long documentId,
            PdmDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var document = await dbContext.Documents.FindAsync(new object[] { documentId }, cancellationToken);
            if (document is null) return Results.NotFound();

            // Check-in normally accompanies a new version upload, 
            // but as an explicit action it just unlocks it.
            document.CheckedOutBy = null;
            document.CheckedOutAt = null;
            
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { message = "Checked in successfully" });
        });

        // ==========================================
        // 任務四：Lifecycle State
        // ==========================================
        app.MapPost("/api/versions/{versionId:long}/change-state", async (
            long versionId,
            ChangeStateRequest request,
            PdmDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var validStates = new[] { "WIP", "InReview", "Released", "Obsolete" };
            if (!validStates.Contains(request.State))
            {
                return EndpointHelpers.ValidationError(nameof(request.State), "Invalid lifecycle state.");
            }

            var version = await dbContext.DocumentVersions.FindAsync(new object[] { versionId }, cancellationToken);
            if (version is null) return Results.NotFound();

            version.LifecycleState = request.State;
            
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { message = "State changed successfully", newState = request.State });
        });

        // ==========================================
        // 任務五：編碼規則維護與派號
        // ==========================================
        app.MapGet("/api/settings/numbering-rules", async (
            PdmDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var rules = await dbContext.NumberingRules
                .OrderBy(x => x.DocumentType)
                .ToListAsync(cancellationToken);
            return Results.Ok(rules);
        });

        app.MapPost("/api/settings/numbering-rules", async (
            PdmNumberingRule request,
            PdmDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var existing = await dbContext.NumberingRules
                .FirstOrDefaultAsync(x => x.DocumentType == request.DocumentType, cancellationToken);

            if (existing is not null)
            {
                existing.Pattern = request.Pattern;
            }
            else
            {
                dbContext.NumberingRules.Add(new PdmNumberingRule
                {
                    DocumentType = request.DocumentType,
                    Pattern = request.Pattern,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { message = "Rules updated successfully." });
        });

        app.MapPost("/api/documents/allocate-number", async (
            AllocateNumberRequest request,
            PdmDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.DocumentType))
            {
                return EndpointHelpers.ValidationError(nameof(request.DocumentType), "DocumentType is required.");
            }

            var rule = await dbContext.NumberingRules
                .FirstOrDefaultAsync(x => x.DocumentType == request.DocumentType, cancellationToken);
            
            if (rule is null)
            {
                // Fallback default rules if not found
                rule = new PdmNumberingRule
                {
                    DocumentType = request.DocumentType,
                    Pattern = request.DocumentType == "Part" ? "PRT-{YYMM}-{SEQ:4}" :
                              request.DocumentType == "Assembly" ? "ASM-{YYMM}-{SEQ:4}" : "DRW-{YYMM}-{SEQ:4}",
                    CreatedAt = DateTimeOffset.UtcNow
                };
            }

            // Parse pattern
            string yymm = DateTimeOffset.UtcNow.ToString("yyMM");
            string pattern = rule.Pattern.Replace("{YYMM}", yymm);

            int seqLength = 4;
            int seqIndex = pattern.IndexOf("{SEQ:", StringComparison.OrdinalIgnoreCase);
            if (seqIndex >= 0)
            {
                int endBracket = pattern.IndexOf('}', seqIndex);
                if (endBracket > seqIndex)
                {
                    string lengthStr = pattern.Substring(seqIndex + 5, endBracket - seqIndex - 5);
                    if (int.TryParse(lengthStr, out int parsedLength))
                    {
                        seqLength = parsedLength;
                    }
                    pattern = pattern.Substring(0, seqIndex) + "{SEQ}" + pattern.Substring(endBracket + 1);
                }
            }
            else if (pattern.Contains("{SEQ}"))
            {
                seqLength = 4; // Default to 4
            }

            string[] parts = pattern.Split("{SEQ}");
            string prefix = parts[0];
            string suffix = parts.Length > 1 ? parts[1] : string.Empty;

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            // Row-level lock on sequence using raw SQL
            // First check if sequence exists
            var seqExists = await dbContext.NumberSequences
                .AnyAsync(x => x.Prefix == prefix, cancellationToken);

            if (!seqExists)
            {
                dbContext.NumberSequences.Add(new PdmNumberSequence 
                {
                    Prefix = prefix,
                    CurrentValue = 0,
                    LastUpdatedAt = DateTimeOffset.UtcNow
                });
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            // In EF Core 7+, retrieving by raw SQL requires the query to map all properties.
            // PostgreSQL specific lock
            FormattableString query = $"SELECT * FROM pdm_number_sequences WHERE prefix = {prefix} FOR UPDATE";
            var lockedSeq = await dbContext.NumberSequences
                .FromSql(query)
                .SingleAsync(cancellationToken);

            lockedSeq.CurrentValue += 1;
            lockedSeq.LastUpdatedAt = DateTimeOffset.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            string allocatedNumber = $"{prefix}{lockedSeq.CurrentValue.ToString($"D{seqLength}")}{suffix}";

            return Results.Ok(new { allocatedNumber });
        });
    }

    private static string SanitizeFileName(string fileName)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        char[] buffer = Path.GetFileName(fileName)
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray();

        return new string(buffer);
    }
}

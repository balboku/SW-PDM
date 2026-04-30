using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;
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
                        (d.PartNumber != null && EF.Functions.ILike(d.PartNumber, searchPattern)));
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
                        CurrentVersionId = d.CurrentVersion != null ? d.CurrentVersion.VersionId : (long?)null,
                        CurrentLifecycleState = d.CurrentVersion != null ? d.CurrentVersion.LifecycleState : null,
                        d.UpdatedAt,
                        d.CheckedOutBy,
                        d.CheckedOutAt
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

        app.MapGet("/api/versions/{versionId:long}/download", async (
            long versionId,
            IPdmRepository repository,
            LocalStorageService storageService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var version = await repository.GetVersionAsync(versionId, cancellationToken);
                if (version is null) return Results.NotFound(new { title = "Version not found" });

                if (string.IsNullOrEmpty(version.StorageFileId))
                {
                    return Results.BadRequest(new { title = "File has no storage ID (not uploaded)" });
                }

                var filePath = storageService.GetFilePath(version.StorageFileId);
                return Results.File(filePath, "application/octet-stream", version.OriginalFileName);
            }
            catch (Exception ex)
            {
                return EndpointHelpers.ToProblem(ex);
            }
        });

        app.MapGet("/api/versions/{versionId:long}/thumbnail", async (
            long versionId,
            PdmDbContext dbContext,
            LocalStorageService storageService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var version = await dbContext.DocumentVersions
                    .AsNoTracking()
                    .Where(x => x.VersionId == versionId)
                    .Select(x => new { x.ThumbnailStorageId })
                    .SingleOrDefaultAsync(cancellationToken);

                if (version is null)
                {
                    return Results.NotFound(new { title = "Version not found" });
                }

                if (string.IsNullOrWhiteSpace(version.ThumbnailStorageId))
                {
                    return Results.NotFound(new { title = "Thumbnail not found" });
                }

                string thumbnailPath = storageService.GetFilePath(version.ThumbnailStorageId);
                return Results.File(thumbnailPath, "image/png");
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
                        if (string.IsNullOrEmpty(file.StorageFileId))
                        {
                            downloadIssues.Add($"File '{file.OriginalFileName}' has no storage ID.");
                            continue;
                        }

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
        app.MapGet("/api/documents/{documentId:long}/checkout-references", async (
            long documentId,
            IPdmRepository repository,
            PdmDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var document = await repository.GetDocumentAsync(documentId, cancellationToken);
                if (document == null || document.CurrentVersionId == null) return Results.NotFound();

                // 1. 取得所有子零件 (Recursive Children)
                var children = await repository.GetPackageClosureAsync(document.CurrentVersionId.Value, cancellationToken);
                
                // 2. 取得所有父階或相關圖面 (Where-Used / Drawings)
                var parents = await repository.GetWhereUsedAsync(document.CurrentVersionId.Value, cancellationToken);

                var relationVersionIds = children
                    .Select(x => x.VersionId)
                    .Concat(parents.Select(x => x.VersionId))
                    .Distinct()
                    .ToList();

                var checkoutRows = await dbContext.Documents
                    .AsNoTracking()
                    .Where(x => x.CurrentVersionId.HasValue && relationVersionIds.Contains(x.CurrentVersionId.Value))
                    .Select(x => new
                    {
                        VersionId = x.CurrentVersionId!.Value,
                        x.CheckedOutBy,
                        x.CheckedOutAt
                    })
                    .ToListAsync(cancellationToken);

                var checkoutStates = checkoutRows.ToDictionary(x => x.VersionId);
                object ToRelationDto(PdmPackageFile file)
                {
                    checkoutStates.TryGetValue(file.VersionId, out var checkout);
                    return new
                    {
                        file.VersionId,
                        file.DocumentType,
                        file.StorageFileId,
                        file.OriginalFileName,
                        file.SourceFilePath,
                        file.VaultRelativePath,
                        file.Depth,
                        CheckedOutBy = checkout?.CheckedOutBy,
                        CheckedOutAt = checkout?.CheckedOutAt
                    };
                }

                return Results.Ok(new {
                    document = document,
                    references = children
                        .Where(x => x.VersionId != document.CurrentVersionId)
                        .OrderBy(x => x.Depth)
                        .Select(ToRelationDto)
                        .ToList(),
                    whereUsed = parents
                        .Select(ToRelationDto)
                        .ToList()
                });
            }
            catch (Exception ex)
            {
                return EndpointHelpers.ToProblem(ex);
            }
        });

        app.MapPost("/api/documents/{documentId:long}/checkout", async (
            long documentId,
            [FromQuery] bool forceIncludeRelations,
            CheckOutRequest request,
            IPdmRepository repository,
            PdmDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.CheckOutBy))
            {
                return EndpointHelpers.ValidationError(nameof(request.CheckOutBy), "CheckOutBy provider is required.");
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var document = await dbContext.Documents.FindAsync(new object[] { documentId }, cancellationToken);
                if (document is null) return Results.NotFound();

                if (!string.IsNullOrWhiteSpace(document.CheckedOutBy))
                {
                    return Results.BadRequest($"Document is already checked out by {document.CheckedOutBy}.");
                }

                var checkedOutAt = DateTimeOffset.UtcNow;
                document.CheckedOutBy = request.CheckOutBy;
                document.CheckedOutAt = checkedOutAt;

                if (forceIncludeRelations && document.CurrentVersionId.HasValue)
                {
                    var children = await repository.GetPackageClosureAsync(document.CurrentVersionId.Value, cancellationToken);
                    var parents = await repository.GetWhereUsedAsync(document.CurrentVersionId.Value, cancellationToken);

                    var relationVersionIds = children
                        .Select(x => x.VersionId)
                        .Concat(parents.Select(x => x.VersionId))
                        .Where(versionId => versionId != document.CurrentVersionId.Value)
                        .Distinct()
                        .ToList();

                    if (relationVersionIds.Count > 0)
                    {
                        var relatedDocuments = await dbContext.Documents
                            .Where(x => x.CurrentVersionId.HasValue && relationVersionIds.Contains(x.CurrentVersionId.Value))
                            .ToListAsync(cancellationToken);

                        var blockedDocuments = relatedDocuments
                            .Where(x => !string.IsNullOrWhiteSpace(x.CheckedOutBy) &&
                                        !string.Equals(x.CheckedOutBy, request.CheckOutBy, StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        if (blockedDocuments.Count > 0)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            return Results.Conflict(new
                            {
                                title = "Checkout conflict",
                                detail = "Cannot check out the complete relation chain because one or more related documents are already checked out by another user.",
                                conflicts = blockedDocuments.Select(x => new
                                {
                                    x.DocumentId,
                                    x.FileName,
                                    x.CheckedOutBy,
                                    x.CheckedOutAt
                                })
                            });
                        }

                        foreach (var relatedDocument in relatedDocuments.Where(x => string.IsNullOrWhiteSpace(x.CheckedOutBy)))
                        {
                            relatedDocument.CheckedOutBy = request.CheckOutBy;
                            relatedDocument.CheckedOutAt = checkedOutAt;
                        }
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return Results.Ok(new { message = "Checked out successfully", checkedOutBy = document.CheckedOutBy });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return EndpointHelpers.ToProblem(ex);
            }
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
        // 任務五：編碼規則維護 (派號功能已移除)
        // 系統改為完全依賴 SolidWorks CAD 檔案內部的 PartNumber 屬性。
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

        // POST /api/documents/allocate-number 已移除。
        // 系統不再自動派發圖號；料號由 SolidWorks CAD 自訂屬性 (PartNumber) 決定。
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

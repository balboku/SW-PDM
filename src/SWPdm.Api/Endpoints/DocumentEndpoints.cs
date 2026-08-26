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
using Microsoft.Extensions.Logging;

namespace SWPdm.Api.Endpoints;

public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this WebApplication app)
    {
        // 階段 1：搜尋圖檔的 API
        app.MapGet("/api/documents/search", async (
            string? query,
            string? documentType,
            string? status,
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
                        (d.PartNumber != null && EF.Functions.ILike(d.PartNumber, searchPattern)) ||
                        dbContext.CustomProperties.Any(p =>
                            p.VersionId == d.CurrentVersionId &&
                            p.ConfigurationName == string.Empty &&
                            p.PropertyName == "圖號" &&
                            p.PropertyValue != null &&
                            EF.Functions.ILike(p.PropertyValue, searchPattern)));
                }

                if (!string.IsNullOrWhiteSpace(documentType) && documentType != "All")
                {
                    queryable = queryable.Where(d => d.DocumentType == documentType);
                }

                if (!string.IsNullOrWhiteSpace(status))
                {
                    if (status == "CheckedOut")
                    {
                        queryable = queryable.Where(d => d.CheckedOutBy != null);
                    }
                    else if (status == "Available")
                    {
                        queryable = queryable.Where(d => d.CheckedOutBy == null);
                    }
                }

                var results = await queryable
                    .OrderByDescending(d => d.UpdatedAt)
                    .Take(50)
                    .Select(d => new
                    {
                        d.DocumentId,
                        d.FileName,
                        DrawingNumber = d.CurrentVersion != null
                            ? dbContext.CustomProperties
                                .Where(p =>
                                    p.VersionId == d.CurrentVersion.VersionId &&
                                    p.ConfigurationName == string.Empty &&
                                    p.PropertyName == "圖號")
                                .Select(p => p.PropertyValue)
                                .FirstOrDefault()
                            : null,
                        d.PartNumber,
                        d.DocumentType,
                        RevisionLabel = d.CurrentVersion != null
                            ? d.CurrentVersion.RevisionLabel
                              ?? dbContext.CustomProperties
                                  .Where(p =>
                                      p.VersionId == d.CurrentVersion.VersionId &&
                                      (p.PropertyName == "Revision" ||
                                       p.PropertyName == "Rev" ||
                                       p.PropertyName == "版次"))
                                  .Select(p => p.PropertyValue)
                                  .FirstOrDefault()
                              ?? d.RevisionLabel
                            : d.RevisionLabel,
                        d.Material,
                        CurrentVersionNo = d.CurrentVersion != null ? d.CurrentVersion.VersionNo : (int?)null,
                        CurrentVersionId = d.CurrentVersion != null ? d.CurrentVersion.VersionId : (long?)null,
                        CurrentLifecycleState = d.CurrentVersion != null ? d.CurrentVersion.LifecycleState : null,
                        ReferenceUpdateCount = dbContext.BomOccurrences
                            .Where(x =>
                                d.CurrentVersionId.HasValue &&
                                x.ParentVersionId == d.CurrentVersionId.Value &&
                                x.ChildVersionId.HasValue &&
                                x.ChildVersion!.Document.CurrentVersion != null &&
                                x.ChildVersion.Document.CurrentVersion.VersionNo > x.ChildVersion.VersionNo)
                            .Select(x => x.ChildVersion!.DocumentId)
                            .Distinct()
                            .Count(),
                        AffectedReferenceOccurrenceCount = dbContext.BomOccurrences
                            .Count(x =>
                                d.CurrentVersionId.HasValue &&
                                x.ParentVersionId == d.CurrentVersionId.Value &&
                                x.ChildVersionId.HasValue &&
                                x.ChildVersion!.Document.CurrentVersion != null &&
                                x.ChildVersion.Document.CurrentVersion.VersionNo > x.ChildVersion.VersionNo),
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

        app.MapGet("/api/documents/{documentId:long}/reference-updates", async (
            long documentId,
            PdmDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var parentDocument = await dbContext.Documents
                    .AsNoTracking()
                    .Where(x => x.DocumentId == documentId)
                    .Select(x => new
                    {
                        x.DocumentId,
                        x.DocumentType,
                        x.CurrentVersionId
                    })
                    .SingleOrDefaultAsync(cancellationToken);

                if (parentDocument is null)
                {
                    return Results.NotFound(new
                    {
                        title = "Document not found",
                        detail = $"No document found for documentId={documentId}."
                    });
                }

                if (!parentDocument.CurrentVersionId.HasValue)
                {
                    return Results.Ok(new
                    {
                        parentDocument.DocumentId,
                        parentDocument.DocumentType,
                        parentDocument.CurrentVersionId,
                        checkedOccurrenceCount = 0,
                        affectedOccurrenceCount = 0,
                        updateCount = 0,
                        hasUpdates = false,
                        updates = Array.Empty<object>()
                    });
                }

                long parentVersionId = parentDocument.CurrentVersionId.Value;
                var referenceRows = await dbContext.BomOccurrences
                    .AsNoTracking()
                    .Where(x => x.ParentVersionId == parentVersionId && x.ChildVersionId.HasValue)
                    .Select(x => new
                    {
                        x.BomOccurrenceId,
                        x.OccurrencePath,
                        ReferencedVersionId = x.ChildVersionId!.Value,
                        ReferencedVersionNo = x.ChildVersion!.VersionNo,
                        ReferencedRevisionLabel = x.ChildVersion.RevisionLabel,
                        ReferencedFileName = x.ChildVersion.OriginalFileName,
                        ChildDocumentId = x.ChildVersion.DocumentId,
                        ChildDocumentType = x.ChildVersion.Document.DocumentType,
                        ChildPartNumber = x.ChildVersion.Document.PartNumber,
                        CurrentVersionId = x.ChildVersion.Document.CurrentVersionId,
                        CurrentVersionNo = x.ChildVersion.Document.CurrentVersion != null
                            ? x.ChildVersion.Document.CurrentVersion.VersionNo
                            : (int?)null,
                        CurrentRevisionLabel = x.ChildVersion.Document.CurrentVersion != null
                            ? x.ChildVersion.Document.CurrentVersion.RevisionLabel
                            : null,
                        CurrentFileName = x.ChildVersion.Document.CurrentVersion != null
                            ? x.ChildVersion.Document.CurrentVersion.OriginalFileName
                            : null
                    })
                    .ToListAsync(cancellationToken);

                var updates = referenceRows
                    .Where(x =>
                        x.CurrentVersionId.HasValue &&
                        x.CurrentVersionNo.HasValue &&
                        x.CurrentVersionNo.Value > x.ReferencedVersionNo)
                    .GroupBy(x => new
                    {
                        x.ChildDocumentId,
                        x.ChildDocumentType,
                        x.ChildPartNumber,
                        x.ReferencedVersionId,
                        x.ReferencedVersionNo,
                        x.ReferencedRevisionLabel,
                        x.ReferencedFileName,
                        x.CurrentVersionId,
                        x.CurrentVersionNo,
                        x.CurrentRevisionLabel,
                        x.CurrentFileName
                    })
                    .Select(group => new
                    {
                        group.Key.ChildDocumentId,
                        group.Key.ChildDocumentType,
                        group.Key.ChildPartNumber,
                        group.Key.ReferencedVersionId,
                        group.Key.ReferencedVersionNo,
                        group.Key.ReferencedRevisionLabel,
                        group.Key.ReferencedFileName,
                        group.Key.CurrentVersionId,
                        group.Key.CurrentVersionNo,
                        group.Key.CurrentRevisionLabel,
                        group.Key.CurrentFileName,
                        AffectedOccurrenceCount = group.Count(),
                        OccurrencePaths = group
                            .Select(x => x.OccurrencePath)
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                            .ToArray()
                    })
                    .OrderBy(x => x.ChildDocumentType, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.CurrentFileName, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return Results.Ok(new
                {
                    parentDocument.DocumentId,
                    parentDocument.DocumentType,
                    parentDocument.CurrentVersionId,
                    checkedOccurrenceCount = referenceRows.Count,
                    affectedOccurrenceCount = updates.Sum(x => x.AffectedOccurrenceCount),
                    updateCount = updates.Length,
                    hasUpdates = updates.Length > 0,
                    updates
                });
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

        app.MapGet("/api/assemblies/{rootVersionId:long}/check-updates", async (
            long rootVersionId,
            IPdmRepository repository,
            PdmDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            try
            {
                IReadOnlyList<PdmPackageFile> files =
                    await repository.GetPackageClosureAsync(rootVersionId, cancellationToken);

                if (files.Count == 0)
                {
                    return Results.NotFound(new
                    {
                        title = "Assembly not found",
                        detail = $"No package files found for rootVersionId={rootVersionId}."
                    });
                }

                long[] versionIds = files
                    .Select(x => x.VersionId)
                    .Distinct()
                    .ToArray();

                var packageVersions = await dbContext.DocumentVersions
                    .AsNoTracking()
                    .Where(x => versionIds.Contains(x.VersionId))
                    .Select(x => new
                    {
                        x.VersionId,
                        x.DocumentId,
                        x.Document.CurrentVersionId,
                        x.OriginalFileName,
                        x.VersionNo,
                        x.RevisionLabel
                    })
                    .ToListAsync(cancellationToken);

                bool hasUpdates = packageVersions.Any(x =>
                    x.CurrentVersionId.HasValue &&
                    x.VersionId < x.CurrentVersionId.Value);

                long[] documentIds = packageVersions
                    .Where(x => x.CurrentVersionId.HasValue && x.VersionId < x.CurrentVersionId.Value)
                    .Select(x => x.DocumentId)
                    .Distinct()
                    .ToArray();

                var availableVersions = await dbContext.DocumentVersions
                    .AsNoTracking()
                    .Where(x => documentIds.Contains(x.DocumentId))
                    .OrderByDescending(x => x.VersionNo)
                    .Select(x => new
                    {
                        x.DocumentId,
                        x.VersionId,
                        x.VersionNo,
                        x.RevisionLabel,
                        x.OriginalFileName,
                        x.CreatedAt
                    })
                    .ToListAsync(cancellationToken);

                var updateItems = packageVersions
                    .Where(x => x.CurrentVersionId.HasValue && x.VersionId < x.CurrentVersionId.Value)
                    .Select(x => new
                    {
                        SourceVersionId = x.VersionId,
                        x.DocumentId,
                        x.OriginalFileName,
                        PackageVersionNo = x.VersionNo,
                        PackageRevisionLabel = x.RevisionLabel,
                        CurrentVersionId = x.CurrentVersionId,
                        Versions = availableVersions
                            .Where(v => v.DocumentId == x.DocumentId)
                            .Select(v => new
                            {
                                v.VersionId,
                                v.VersionNo,
                                v.RevisionLabel,
                                v.OriginalFileName,
                                v.CreatedAt,
                                IsPackageVersion = v.VersionId == x.VersionId,
                                IsCurrentVersion = v.VersionId == x.CurrentVersionId
                            })
                            .ToArray()
                    })
                    .ToArray();

                IReadOnlyList<RelatedDrawingPackageFile> relatedDrawings =
                    await GetRelatedDrawingsAsync(files, dbContext, cancellationToken);

                return Results.Ok(new
                {
                    hasUpdates,
                    updates = updateItems,
                    relatedDrawings = relatedDrawings.Select(ToRelatedDrawingDto).ToArray()
                });
            }
            catch (Exception ex)
            {
                return EndpointHelpers.ToProblem(ex);
            }
        });

        app.MapGet("/api/assemblies/{rootVersionId:long}/download-zip", async (
            long rootVersionId,
            IPdmRepository repository,
            PdmDbContext dbContext,
            LocalStorageService storageService,
            CancellationToken cancellationToken,
            [FromQuery] bool useLatest = false,
            [FromQuery] string[]? versionOverrides = null,
            [FromQuery] bool includeDrawings = false) =>
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

            if (useLatest)
            {
                files = await ResolveLatestPackageFilesAsync(files, dbContext, cancellationToken);
            }
            else if (versionOverrides is { Length: > 0 })
            {
                files = await ResolveOverriddenPackageFilesAsync(files, versionOverrides, dbContext, cancellationToken);
            }

            if (includeDrawings)
            {
                IReadOnlyList<RelatedDrawingPackageFile> relatedDrawings =
                    await GetRelatedDrawingsAsync(files, dbContext, cancellationToken);

                files = files
                    .Concat(relatedDrawings.Select(x => x.File))
                    .GroupBy(x => x.DocumentId)
                    .Select(x => x.First())
                    .ToArray();
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

        app.MapGet("/api/documents/{documentId:long}/relations", async (
            long documentId,
            IPdmRepository repository,
            PdmDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var document = await dbContext.Documents
                    .AsNoTracking()
                    .Where(x => x.DocumentId == documentId)
                    .Select(x => new
                    {
                        x.DocumentId,
                        x.CurrentVersionId
                    })
                    .SingleOrDefaultAsync(cancellationToken);

                if (document is null || !document.CurrentVersionId.HasValue)
                {
                    return Results.NotFound(new
                    {
                        title = "Document relations not found",
                        detail = "找不到目前版本，請重新整理圖檔中心後再試。"
                    });
                }

                IReadOnlyList<PdmPackageFile> closure =
                    await repository.GetPackageClosureAsync(document.CurrentVersionId.Value, cancellationToken);
                IReadOnlyList<RelatedDrawingPackageFile> drawings =
                    await GetRelatedDrawingsAsync(closure, dbContext, cancellationToken);
                IReadOnlyList<PdmPackageFile> whereUsed =
                    await repository.GetWhereUsedAsync(document.CurrentVersionId.Value, cancellationToken);

                var identityOrigin = await dbContext.DocumentIdentityChanges
                    .AsNoTracking()
                    .Where(x => x.TargetDocumentId == documentId)
                    .Select(x => new
                    {
                        x.IdentityChangeId,
                        x.SourceDocumentId,
                        x.SourceVersionId,
                        x.TargetDocumentId,
                        x.OldPartNumber,
                        x.NewPartNumber,
                        x.ChangeReason,
                        x.ChangedBy,
                        x.CreatedAt,
                        SourceFileName = x.SourceDocument.FileName,
                        SourceDocumentType = x.SourceDocument.DocumentType
                    })
                    .SingleOrDefaultAsync(cancellationToken);

                var derivedDocuments = await dbContext.DocumentIdentityChanges
                    .AsNoTracking()
                    .Where(x => x.SourceDocumentId == documentId)
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => new
                    {
                        x.IdentityChangeId,
                        x.SourceDocumentId,
                        x.SourceVersionId,
                        x.TargetDocumentId,
                        x.OldPartNumber,
                        x.NewPartNumber,
                        x.ChangeReason,
                        x.ChangedBy,
                        x.CreatedAt,
                        TargetFileName = x.TargetDocument.FileName,
                        TargetDocumentType = x.TargetDocument.DocumentType,
                        TargetVersionId = x.TargetDocument.CurrentVersionId
                    })
                    .ToArrayAsync(cancellationToken);

                return Results.Ok(new
                {
                    references = closure
                        .Where(x => x.VersionId != document.CurrentVersionId.Value)
                        .OrderBy(x => x.Depth)
                        .ToArray(),
                    drawings = drawings
                        .Where(x => x.File.DocumentId != documentId)
                        .Select(ToRelatedDrawingDto)
                        .ToArray(),
                    whereUsed,
                    identityOrigin,
                    derivedDocuments
                });
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
            ILogger<Program> _logger,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var document = await repository.GetDocumentAsync(documentId, cancellationToken);
                if (document == null || document.CurrentVersionId == null) return Results.NotFound();

                // 1. 取得所有子零件 (Recursive Children)
                var children = await repository.GetPackageClosureAsync(document.CurrentVersionId.Value, cancellationToken);

                // 2. 取得所有子項目關聯的工程圖 (Drawings)
                var childDocIds = children.Select(x => x.DocumentId).Distinct().ToList();
                var childFilenames = children.Select(x => x.OriginalFileName.ToLower()).ToList();
                
                _logger.LogInformation("Checking drawings for {Count} child documents", childDocIds.Count);

                // A. 透過「文件 ID」找工程圖 (不論參考哪一個版本)
                var drawingIdsFromLinks = await dbContext.BomOccurrences
                    .Where(bom => bom.ChildVersionId.HasValue)
                    .Join(dbContext.DocumentVersions,
                          bom => bom.ChildVersionId!.Value,
                          cv => cv.VersionId,
                          (bom, cv) => new { bom.ParentVersionId, cv.DocumentId })
                    .Where(x => childDocIds.Contains(x.DocumentId))
                    .Join(dbContext.DocumentVersions,
                          x => x.ParentVersionId,
                          pv => pv.VersionId,
                          (x, pv) => new { pv.VersionId, pv.Document.DocumentType })
                    .Where(x => x.DocumentType == "Drawing")
                    .Select(x => x.VersionId)
                    .ToListAsync(cancellationToken);

                // B. 透過檔名匹配 (Fallback for Missing links)
                var drawingsMissingLinks = await dbContext.BomOccurrences
                    .Where(bom => bom.ChildVersionId == null)
                    .Join(dbContext.DocumentVersions,
                          bom => bom.ParentVersionId,
                          v => v.VersionId,
                          (bom, v) => new { v.VersionId, v.Document.DocumentType, bom.SourceReferencePath })
                    .Where(x => x.DocumentType == "Drawing")
                    .ToListAsync(cancellationToken);

                var drawingIdsFromFilename = drawingsMissingLinks
                    .Where(x => !string.IsNullOrEmpty(x.SourceReferencePath) && 
                                childFilenames.Contains(Path.GetFileName(x.SourceReferencePath).ToLower()))
                    .Select(x => x.VersionId)
                    .ToList();

                var relatedDrawingVersionIds = drawingIdsFromLinks
                    .Concat(drawingIdsFromFilename)
                    .Distinct()
                    .ToList();

                _logger.LogInformation("Found {Count} related drawings", relatedDrawingVersionIds.Count);

                var drawingFiles = await dbContext.DocumentVersions
                    .Where(v => relatedDrawingVersionIds.Contains(v.VersionId))
                    .Select(v => new PdmPackageFile(
                        v.DocumentId,
                        v.VersionId,
                        v.Document.DocumentType,
                        v.StorageFileId,
                        v.OriginalFileName,
                        v.SourceFilePath,
                        v.VaultRelativePath,
                        -1 // 標記為工程圖
                    ))
                    .ToListAsync(cancellationToken);

                // 3. 取得所有父階 (Where-Used) - 僅供前端純顯示參考，不參與鎖定
                var parents = await repository.GetWhereUsedAsync(document.CurrentVersionId.Value, cancellationToken);

                // 4. 重組鎖定清單：只包含 Children 與 Drawings，排除 Parents (防止鎖定過多無關父組件)
                var relationVersionIds = children.Select(x => x.VersionId)
                    .Concat(relatedDrawingVersionIds)
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
                    drawings = drawingFiles
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
            ILogger<Program> _logger,
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
                    // 1. 取得所有子項目 (Recursive Children)
                    var children = await repository.GetPackageClosureAsync(document.CurrentVersionId.Value, cancellationToken);

                    // 2. 取得所有子項目關聯的工程圖 (Drawings)
                    var childDocIds = children.Select(x => x.DocumentId).Distinct().ToList();
                    var childFilenames = children.Select(x => x.OriginalFileName.ToLower()).ToList();
                    
                    _logger.LogInformation("Checking drawings for {Count} child documents during checkout", childDocIds.Count);

                    // A. 透過「文件 ID」找工程圖 (不論參考哪一個版本)
                    var drawingIdsFromLinks = await dbContext.BomOccurrences
                        .Where(bom => bom.ChildVersionId.HasValue)
                        .Join(dbContext.DocumentVersions,
                              bom => bom.ChildVersionId!.Value,
                              cv => cv.VersionId,
                              (bom, cv) => new { bom.ParentVersionId, cv.DocumentId })
                        .Where(x => childDocIds.Contains(x.DocumentId))
                        .Join(dbContext.DocumentVersions,
                              x => x.ParentVersionId,
                              pv => pv.VersionId,
                              (x, pv) => new { pv.VersionId, pv.Document.DocumentType })
                        .Where(x => x.DocumentType == "Drawing")
                        .Select(x => x.VersionId)
                        .ToListAsync(cancellationToken);

                    // B. 透過檔名匹配 (Fallback for Missing links)
                    var drawingsMissingLinks = await dbContext.BomOccurrences
                        .Where(bom => bom.ChildVersionId == null)
                        .Join(dbContext.DocumentVersions,
                              bom => bom.ParentVersionId,
                              v => v.VersionId,
                              (bom, v) => new { v.VersionId, v.Document.DocumentType, bom.SourceReferencePath })
                        .Where(x => x.DocumentType == "Drawing")
                        .ToListAsync(cancellationToken);

                    var drawingIdsFromFilename = drawingsMissingLinks
                        .Where(x => !string.IsNullOrEmpty(x.SourceReferencePath) && 
                                    childFilenames.Contains(Path.GetFileName(x.SourceReferencePath).ToLower()))
                        .Select(x => x.VersionId)
                        .ToList();

                    var relatedDrawingVersionIds = drawingIdsFromLinks
                        .Concat(drawingIdsFromFilename)
                        .Distinct()
                        .ToList();

                    _logger.LogInformation("Found {Count} related drawings during checkout", relatedDrawingVersionIds.Count);

                    // 3. 重組鎖定清單：合併 Children 與 Drawings，但排除 Parents 以防止鎖定擴散
                    var relationVersionIds = children
                        .Select(x => x.VersionId)
                        .Concat(relatedDrawingVersionIds)
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

    private static async Task<IReadOnlyList<PdmPackageFile>> ResolveLatestPackageFilesAsync(
        IReadOnlyList<PdmPackageFile> files,
        PdmDbContext dbContext,
        CancellationToken cancellationToken)
    {
        long[] sourceVersionIds = files
            .Select(x => x.VersionId)
            .Distinct()
            .ToArray();

        var sourceVersions = await dbContext.DocumentVersions
            .AsNoTracking()
            .Where(x => sourceVersionIds.Contains(x.VersionId))
            .Select(x => new
            {
                SourceVersionId = x.VersionId,
                x.Document.CurrentVersionId
            })
            .ToListAsync(cancellationToken);

        Dictionary<long, long> sourceToCurrentVersionIds = sourceVersions
            .Where(x => x.CurrentVersionId.HasValue)
            .ToDictionary(x => x.SourceVersionId, x => x.CurrentVersionId!.Value);

        long[] currentVersionIds = sourceToCurrentVersionIds.Values
            .Distinct()
            .ToArray();

        var currentVersions = await dbContext.DocumentVersions
            .AsNoTracking()
            .Where(x => currentVersionIds.Contains(x.VersionId))
            .Select(x => new
            {
                x.DocumentId,
                x.VersionId,
                x.Document.DocumentType,
                x.StorageFileId,
                x.OriginalFileName,
                x.SourceFilePath,
                x.VaultRelativePath
            })
            .ToDictionaryAsync(x => x.VersionId, cancellationToken);

        return files
            .Select(file =>
            {
                if (!sourceToCurrentVersionIds.TryGetValue(file.VersionId, out long currentVersionId) ||
                    !currentVersions.TryGetValue(currentVersionId, out var currentVersion))
                {
                    return file;
                }

                return new PdmPackageFile(
                    DocumentId: currentVersion.DocumentId,
                    VersionId: currentVersion.VersionId,
                    DocumentType: currentVersion.DocumentType,
                    StorageFileId: currentVersion.StorageFileId,
                    OriginalFileName: currentVersion.OriginalFileName,
                    SourceFilePath: currentVersion.SourceFilePath,
                    VaultRelativePath: currentVersion.VaultRelativePath,
                    Depth: file.Depth);
            })
            .ToArray();
    }

    private static async Task<IReadOnlyList<RelatedDrawingPackageFile>> GetRelatedDrawingsAsync(
        IReadOnlyCollection<PdmPackageFile> packageFiles,
        PdmDbContext dbContext,
        CancellationToken cancellationToken)
    {
        long[] modelDocumentIds = packageFiles
            .Where(x => !string.Equals(x.DocumentType, "Drawing", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.DocumentId)
            .Distinct()
            .ToArray();

        if (modelDocumentIds.Length == 0)
        {
            return Array.Empty<RelatedDrawingPackageFile>();
        }

        HashSet<string> modelFileNames = packageFiles
            .Where(x => !string.Equals(x.DocumentType, "Drawing", StringComparison.OrdinalIgnoreCase))
            .Select(x => Path.GetFileName(x.OriginalFileName))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        long[] directlyLinkedDrawingDocumentIds = await dbContext.BomOccurrences
            .AsNoTracking()
            .Where(x => x.ChildVersionId.HasValue)
            .Join(
                dbContext.DocumentVersions,
                bom => bom.ChildVersionId!.Value,
                childVersion => childVersion.VersionId,
                (bom, childVersion) => new
                {
                    bom.ParentVersionId,
                    ChildDocumentId = childVersion.DocumentId
                })
            .Where(x => modelDocumentIds.Contains(x.ChildDocumentId))
            .Join(
                dbContext.DocumentVersions,
                link => link.ParentVersionId,
                parentVersion => parentVersion.VersionId,
                (link, parentVersion) => new
                {
                    DrawingDocumentId = parentVersion.DocumentId,
                    parentVersion.Document.DocumentType
                })
            .Where(x => x.DocumentType == "Drawing")
            .Select(x => x.DrawingDocumentId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        var unresolvedDrawingLinks = await dbContext.BomOccurrences
            .AsNoTracking()
            .Where(x => !x.ChildVersionId.HasValue && !string.IsNullOrEmpty(x.SourceReferencePath))
            .Join(
                dbContext.DocumentVersions,
                bom => bom.ParentVersionId,
                parentVersion => parentVersion.VersionId,
                (bom, parentVersion) => new
                {
                    DrawingDocumentId = parentVersion.DocumentId,
                    parentVersion.Document.DocumentType,
                    bom.SourceReferencePath
                })
            .Where(x => x.DocumentType == "Drawing")
            .ToListAsync(cancellationToken);

        long[] filenameMatchedDrawingDocumentIds = unresolvedDrawingLinks
            .Where(x => modelFileNames.Contains(Path.GetFileName(x.SourceReferencePath)))
            .Select(x => x.DrawingDocumentId)
            .Distinct()
            .ToArray();

        Dictionary<long, string> matchMethods = directlyLinkedDrawingDocumentIds
            .ToDictionary(x => x, _ => "DocumentId");

        foreach (long drawingDocumentId in filenameMatchedDrawingDocumentIds)
        {
            matchMethods.TryAdd(drawingDocumentId, "FilenameFallback");
        }

        if (matchMethods.Count == 0)
        {
            return Array.Empty<RelatedDrawingPackageFile>();
        }

        long[] drawingDocumentIds = matchMethods.Keys.ToArray();
        var currentDrawingVersions = await dbContext.DocumentVersions
            .AsNoTracking()
            .Where(x =>
                drawingDocumentIds.Contains(x.DocumentId) &&
                x.Document.CurrentVersionId == x.VersionId)
            .Select(x => new
            {
                x.DocumentId,
                x.VersionId,
                x.Document.DocumentType,
                x.StorageFileId,
                x.OriginalFileName,
                x.SourceFilePath,
                x.VaultRelativePath
            })
            .OrderBy(x => x.OriginalFileName)
            .ToListAsync(cancellationToken);

        return currentDrawingVersions
            .Select(x => new RelatedDrawingPackageFile(
                new PdmPackageFile(
                    DocumentId: x.DocumentId,
                    VersionId: x.VersionId,
                    DocumentType: x.DocumentType,
                    StorageFileId: x.StorageFileId,
                    OriginalFileName: x.OriginalFileName,
                    SourceFilePath: x.SourceFilePath,
                    VaultRelativePath: x.VaultRelativePath,
                    Depth: -1),
                MatchMethod: matchMethods[x.DocumentId]))
            .ToArray();
    }

    private static object ToRelatedDrawingDto(RelatedDrawingPackageFile drawing)
    {
        return new
        {
            drawing.File.DocumentId,
            drawing.File.VersionId,
            drawing.File.DocumentType,
            drawing.File.StorageFileId,
            drawing.File.OriginalFileName,
            drawing.File.SourceFilePath,
            drawing.File.VaultRelativePath,
            drawing.MatchMethod
        };
    }

    private static async Task<IReadOnlyList<PdmPackageFile>> ResolveOverriddenPackageFilesAsync(
        IReadOnlyList<PdmPackageFile> files,
        IReadOnlyCollection<string> versionOverrides,
        PdmDbContext dbContext,
        CancellationToken cancellationToken)
    {
        Dictionary<long, long> overrideMap = new();
        foreach (string overrideValue in versionOverrides)
        {
            string[] parts = overrideValue.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 ||
                !long.TryParse(parts[0], out long sourceVersionId) ||
                !long.TryParse(parts[1], out long selectedVersionId))
            {
                continue;
            }

            overrideMap[sourceVersionId] = selectedVersionId;
        }

        if (overrideMap.Count == 0)
        {
            return files;
        }

        long[] packageVersionIds = files
            .Select(x => x.VersionId)
            .Distinct()
            .ToArray();

        var packageDocuments = await dbContext.DocumentVersions
            .AsNoTracking()
            .Where(x => packageVersionIds.Contains(x.VersionId))
            .Select(x => new
            {
                x.VersionId,
                x.DocumentId
            })
            .ToDictionaryAsync(x => x.VersionId, x => x.DocumentId, cancellationToken);

        long[] selectedVersionIds = overrideMap.Values
            .Distinct()
            .ToArray();

        var selectedVersions = await dbContext.DocumentVersions
            .AsNoTracking()
            .Where(x => selectedVersionIds.Contains(x.VersionId))
            .Select(x => new
            {
                x.VersionId,
                x.DocumentId,
                x.Document.DocumentType,
                x.StorageFileId,
                x.OriginalFileName,
                x.SourceFilePath,
                x.VaultRelativePath
            })
            .ToDictionaryAsync(x => x.VersionId, cancellationToken);

        return files
            .Select(file =>
            {
                if (!overrideMap.TryGetValue(file.VersionId, out long selectedVersionId) ||
                    !packageDocuments.TryGetValue(file.VersionId, out long packageDocumentId) ||
                    !selectedVersions.TryGetValue(selectedVersionId, out var selectedVersion) ||
                    selectedVersion.DocumentId != packageDocumentId)
                {
                    return file;
                }

                return new PdmPackageFile(
                    DocumentId: selectedVersion.DocumentId,
                    VersionId: selectedVersion.VersionId,
                    DocumentType: selectedVersion.DocumentType,
                    StorageFileId: selectedVersion.StorageFileId,
                    OriginalFileName: selectedVersion.OriginalFileName,
                    SourceFilePath: selectedVersion.SourceFilePath,
                    VaultRelativePath: selectedVersion.VaultRelativePath,
                    Depth: file.Depth);
            })
            .ToArray();
    }

    private static string SanitizeFileName(string fileName)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        char[] buffer = Path.GetFileName(fileName)
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray();

        return new string(buffer);
    }

    private sealed record RelatedDrawingPackageFile(
        PdmPackageFile File,
        string MatchMethod);
}

using Microsoft.AspNetCore.Mvc;
using SWPdm.Api.Contracts;
using SWPdm.Api.Services;
using SWPdm.Sample.Services;

namespace SWPdm.Api.Endpoints;

public static class IngestEndpoints
{
    private const int MaxBatchFileCount = 200;
    private const long MaxBatchTotalBytes = 1024L * 1024L * 1024L;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".sldprt",
        ".sldasm",
        ".slddrw"
    };

    public static void MapIngestEndpoints(this WebApplication app)
    {
        app.MapPost("/api/ingest/cad", async (
            IngestCadFileRequest request,
            PdmIngestionService ingestionService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.LocalFilePath))
            {
                return EndpointHelpers.ValidationError(nameof(request.LocalFilePath), "LocalFilePath is required.");
            }

            try
            {
                IngestCadFileResponse response = await ingestionService.IngestAsync(request, cancellationToken);
                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                return EndpointHelpers.ToProblem(ex);
            }
        });

        app.MapPost("/api/web/upload-temp", async (
            IFormFile file,
            CancellationToken cancellationToken) =>
        {
            if (file == null || file.Length == 0)
            {
                return EndpointHelpers.ValidationError(nameof(file), "File is required.");
            }

            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "swpdm_web_uploads");
                Directory.CreateDirectory(tempDir);

                string fileName = Path.GetFileName(file.FileName);
                string extension = Path.GetExtension(fileName);
                if (!SupportedExtensions.Contains(extension))
                {
                    return EndpointHelpers.ValidationError(
                        nameof(file),
                        $"Unsupported file type '{extension}'. Supported types: .sldprt, .sldasm, .slddrw.");
                }

                string tempFilePath = Path.Combine(tempDir, $"{Guid.NewGuid()}_{fileName}");

                await using var stream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await file.CopyToAsync(stream, cancellationToken);
                
                return Results.Ok(new { localFilePath = tempFilePath });
            }
            catch (Exception ex)
            {
                return EndpointHelpers.ToProblem(ex);
            }
        }).DisableAntiforgery();

        app.MapPost("/api/ingest/cad-batch", async (
            HttpRequest httpRequest,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            string batchRoot = Path.GetFullPath(Path.Combine(
                Path.GetTempPath(),
                "swpdm_web_uploads",
                "batches"));
            string batchDirectory = Path.Combine(batchRoot, Guid.NewGuid().ToString("N"));

            try
            {
                IFormCollection form = await httpRequest.ReadFormAsync(cancellationToken);
                IFormFileCollection files = form.Files;

                if (files.Count == 0)
                {
                    return EndpointHelpers.ValidationError("files", "請至少選擇一個 CAD 檔案。");
                }

                if (files.Count > MaxBatchFileCount)
                {
                    return EndpointHelpers.ValidationError(
                        "files",
                        $"單次最多可匯入 {MaxBatchFileCount} 個 CAD 檔案。");
                }

                long totalBytes = files.Sum(x => x.Length);
                if (totalBytes > MaxBatchTotalBytes)
                {
                    return EndpointHelpers.ValidationError(
                        "files",
                        "單次批次檔案總量不可超過 1 GiB。");
                }

                string[] unsupportedFiles = files
                    .Where(x =>
                        x.Length <= 0 ||
                        !SupportedExtensions.Contains(Path.GetExtension(x.FileName)))
                    .Select(x => Path.GetFileName(x.FileName))
                    .ToArray();

                if (unsupportedFiles.Length > 0)
                {
                    return EndpointHelpers.ValidationError(
                        "files",
                        $"批次包含空白或不支援的檔案：{string.Join(", ", unsupportedFiles)}");
                }

                Directory.CreateDirectory(batchDirectory);

                IReadOnlyList<string> relativePaths = form["relativePaths"]
                    .Select(x => x ?? string.Empty)
                    .ToArray();
                List<StagedCadFile> stagedFiles = new(files.Count);
                HashSet<string> destinations = new(StringComparer.OrdinalIgnoreCase);

                for (int index = 0; index < files.Count; index++)
                {
                    IFormFile file = files[index];
                    string suppliedRelativePath =
                        index < relativePaths.Count ? relativePaths[index] : file.FileName;
                    string safeRelativePath = NormalizeBatchRelativePath(
                        suppliedRelativePath,
                        file.FileName);
                    string destinationPath = ResolveBatchDestination(
                        batchDirectory,
                        safeRelativePath);

                    if (!destinations.Add(destinationPath))
                    {
                        return EndpointHelpers.ValidationError(
                            "relativePaths",
                            $"批次中有重複路徑：{safeRelativePath}");
                    }

                    string? destinationFolder = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrWhiteSpace(destinationFolder))
                    {
                        Directory.CreateDirectory(destinationFolder);
                    }

                    await using FileStream stream = new(
                        destinationPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None);
                    await file.CopyToAsync(stream, cancellationToken);
                    stagedFiles.Add(new StagedCadFile(safeRelativePath, destinationPath));
                }

                string uploadedBy = string.IsNullOrWhiteSpace(form["uploadedBy"])
                    ? "User"
                    : form["uploadedBy"].ToString();
                string? changeReason = string.IsNullOrWhiteSpace(form["changeReason"])
                    ? null
                    : form["changeReason"].ToString();
                string[] searchPaths = stagedFiles
                    .Select(x => Path.GetDirectoryName(x.LocalFilePath))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                StagedCadFile[] processingOrder = stagedFiles
                    .OrderBy(x => GetCadProcessingOrder(x.LocalFilePath))
                    .ThenBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                List<BatchIngestFileResult> results = new(processingOrder.Length);

                foreach (StagedCadFile stagedFile in processingOrder)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
                        PdmIngestionService ingestionService =
                            scope.ServiceProvider.GetRequiredService<PdmIngestionService>();
                        IngestCadFileResponse response = await ingestionService.IngestAsync(
                            new IngestCadFileRequest(
                                LocalFilePath: stagedFile.LocalFilePath,
                                DriveFolderId: null,
                                IngestReferencedFiles: false,
                                AdditionalSearchPaths: searchPaths,
                                UploadedBy: uploadedBy,
                                ChangeReason: changeReason,
                                TargetDocumentId: null),
                            cancellationToken);

                        IngestedFileResponse root = response.Files
                            .Single(x => x.VersionId == response.RootVersionId);
                        results.Add(new BatchIngestFileResult(
                            RelativePath: stagedFile.RelativePath,
                            Succeeded: true,
                            DocumentId: root.DocumentId,
                            VersionId: root.VersionId,
                            DocumentType: root.DocumentType,
                            PartNumber: root.PartNumber,
                            VersionNo: root.VersionNo,
                            Issues: response.Issues,
                            ErrorMessage: null));
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        string errorDetail = EndpointHelpers.GetErrorDetail(ex);
                        logger.LogWarning(
                            ex,
                            "Batch ingest failed for {RelativePath}",
                            stagedFile.RelativePath);
                        results.Add(new BatchIngestFileResult(
                            RelativePath: stagedFile.RelativePath,
                            Succeeded: false,
                            DocumentId: null,
                            VersionId: null,
                            DocumentType: null,
                            PartNumber: null,
                            VersionNo: null,
                            Issues: Array.Empty<string>(),
                            ErrorMessage: errorDetail));
                    }
                }

                int succeededCount = results.Count(x => x.Succeeded);
                return Results.Ok(new BatchIngestCadResponse(
                    TotalFileCount: results.Count,
                    SucceededFileCount: succeededCount,
                    FailedFileCount: results.Count - succeededCount,
                    Files: results));
            }
            catch (ArgumentException ex)
            {
                return EndpointHelpers.ValidationError("relativePaths", ex.Message);
            }
            catch (Exception ex)
            {
                return EndpointHelpers.ToProblem(ex);
            }
            finally
            {
                TryDeleteBatchDirectory(batchRoot, batchDirectory, logger);
            }
        }).DisableAntiforgery();

        app.MapPost("/api/storage/upload", async (
            UploadFileRequest request,
            LocalStorageService storageService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.LocalFilePath))
            {
                return EndpointHelpers.ValidationError(nameof(request.LocalFilePath), "LocalFilePath is required.");
            }

            try
            {
                string fileId = await storageService.UploadFileAsync(request.LocalFilePath, "ManualUploads", cancellationToken);

                return Results.Ok(new UploadFileResponse(
                    StorageFileId: fileId,
                    LocalFilePath: request.LocalFilePath));
            }
            catch (Exception ex)
            {
                return EndpointHelpers.ToProblem(ex);
            }
        });

        app.MapPost("/api/storage/download", async (
            DownloadFileRequest request,
            LocalStorageService storageService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.FileId))
            {
                return EndpointHelpers.ValidationError(nameof(request.FileId), "FileId is required.");
            }

            if (string.IsNullOrWhiteSpace(request.DestinationFilePath))
            {
                return EndpointHelpers.ValidationError(nameof(request.DestinationFilePath), "DestinationFilePath is required.");
            }

            try
            {
                string savedPath = await storageService.DownloadFileAsync(
                    request.FileId,
                    request.DestinationFilePath,
                    cancellationToken);

                return Results.Ok(new DownloadFileResponse(
                    FileId: request.FileId,
                    SavedPath: savedPath));
            }
            catch (Exception ex)
            {
                return EndpointHelpers.ToProblem(ex);
            }
        });
    }

    private static string NormalizeBatchRelativePath(
        string suppliedRelativePath,
        string uploadedFileName)
    {
        string candidate = string.IsNullOrWhiteSpace(suppliedRelativePath)
            ? Path.GetFileName(uploadedFileName)
            : suppliedRelativePath.Trim();
        candidate = candidate.Replace('\\', '/');

        if (candidate.StartsWith('/') || Path.IsPathRooted(candidate))
        {
            throw new ArgumentException($"不允許絕對路徑：{candidate}");
        }

        string[] segments = candidate
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0 ||
            segments.Any(x => x is "." or "..") ||
            segments.Any(x => x.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            throw new ArgumentException($"不允許的相對路徑：{candidate}");
        }

        string normalized = Path.Combine(segments);
        string extension = Path.GetExtension(normalized);
        if (!SupportedExtensions.Contains(extension))
        {
            throw new ArgumentException($"不支援的 CAD 類型：{candidate}");
        }

        return normalized;
    }

    private static string ResolveBatchDestination(
        string batchDirectory,
        string relativePath)
    {
        string batchRoot = Path.GetFullPath(batchDirectory);
        string destination = Path.GetFullPath(Path.Combine(batchRoot, relativePath));
        string requiredPrefix = batchRoot.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!destination.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"相對路徑超出批次暫存範圍：{relativePath}");
        }

        return destination;
    }

    private static int GetCadProcessingOrder(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".sldprt" => 0,
            ".sldasm" => 1,
            ".slddrw" => 2,
            _ => 3
        };
    }

    private static void TryDeleteBatchDirectory(
        string batchRoot,
        string batchDirectory,
        ILogger logger)
    {
        try
        {
            string root = Path.GetFullPath(batchRoot).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            string target = Path.GetFullPath(batchDirectory);
            string requiredPrefix = root + Path.DirectorySeparatorChar;

            if (!target.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogError(
                    "Refused to delete batch directory outside root. Root={Root}; Target={Target}",
                    root,
                    target);
                return;
            }

            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clean batch staging directory {BatchDirectory}", batchDirectory);
        }
    }

    private sealed record StagedCadFile(
        string RelativePath,
        string LocalFilePath);
}

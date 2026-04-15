using Microsoft.AspNetCore.Mvc;
using SWPdm.Api.Contracts;
using SWPdm.Api.Services;

namespace SWPdm.Api.Endpoints;

public static class IngestEndpoints
{
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
}

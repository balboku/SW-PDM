namespace SWPdm.Api.Contracts;

public sealed record DownloadFileResponse(
    string FileId,
    string SavedPath);

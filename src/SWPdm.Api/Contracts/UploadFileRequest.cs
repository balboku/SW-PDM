namespace SWPdm.Api.Contracts;

public sealed record UploadFileRequest(
    string LocalFilePath,
    string? DriveFolderId);

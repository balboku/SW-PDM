namespace SWPdm.Api.Contracts;

public sealed record UploadFileResponse(
    string StorageFileId,
    string LocalFilePath);

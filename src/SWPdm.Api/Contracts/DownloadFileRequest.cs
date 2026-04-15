namespace SWPdm.Api.Contracts;

public sealed record DownloadFileRequest(
    string FileId,
    string DestinationFilePath);

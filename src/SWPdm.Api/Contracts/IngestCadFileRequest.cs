namespace SWPdm.Api.Contracts;

public sealed record IngestCadFileRequest(
    string LocalFilePath,
    string? DriveFolderId,
    bool IngestReferencedFiles,
    string[]? AdditionalSearchPaths,
    string? UploadedBy = null);

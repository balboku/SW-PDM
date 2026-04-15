namespace SWPdm.Api.Contracts;

public sealed record IngestedFileResponse(
    string SourceFilePath,
    long DocumentId,
    long VersionId,
    string DocumentType,
    string? PartNumber,
    string StorageFileId,
    bool CreatedDocument,
    int VersionNo);

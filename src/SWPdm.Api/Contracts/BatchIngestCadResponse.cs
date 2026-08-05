namespace SWPdm.Api.Contracts;

public sealed record BatchIngestCadResponse(
    int TotalFileCount,
    int SucceededFileCount,
    int FailedFileCount,
    IReadOnlyList<BatchIngestFileResult> Files);

public sealed record BatchIngestFileResult(
    string RelativePath,
    bool Succeeded,
    long? DocumentId,
    long? VersionId,
    string? DocumentType,
    string? PartNumber,
    int? VersionNo,
    IReadOnlyList<string> Issues,
    string? ErrorMessage);

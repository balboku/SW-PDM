namespace SWPdm.Sample.Data.Models;

public sealed record PdmDocumentVersionSummary(
    long VersionId,
    int VersionNo,
    string? RevisionLabel,
    string OriginalFileName,
    string StorageFileId,
    DateTimeOffset CreatedAt);

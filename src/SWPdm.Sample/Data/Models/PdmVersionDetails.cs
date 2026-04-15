namespace SWPdm.Sample.Data.Models;

public sealed record PdmVersionDetails(
    long VersionId,
    long DocumentId,
    int VersionNo,
    string? RevisionLabel,
    string StorageFileId,
    string OriginalFileName,
    string SourceFilePath,
    string VaultRelativePath,
    string? ChecksumSha256,
    long? FileSizeBytes,
    DateTimeOffset? SourceLastWriteUtc,
    DateTimeOffset? ParsedAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PdmCustomPropertyData> CustomProperties,
    IReadOnlyList<PdmBomLinkData> ImmediateChildren);

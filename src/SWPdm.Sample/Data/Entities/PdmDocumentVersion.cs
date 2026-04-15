namespace SWPdm.Sample.Data.Entities;

public sealed class PdmDocumentVersion
{
    public long VersionId { get; set; }

    public long DocumentId { get; set; }

    public int VersionNo { get; set; }

    public string? RevisionLabel { get; set; }

    public string StorageFileId { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string SourceFilePath { get; set; } = string.Empty;

    public string VaultRelativePath { get; set; } = string.Empty;

    public string? ChecksumSha256 { get; set; }

    public long? FileSizeBytes { get; set; }

    public DateTimeOffset? SourceLastWriteUtc { get; set; }

    public DateTimeOffset? ParsedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string LifecycleState { get; set; } = "WIP";

    public PdmDocument Document { get; set; } = null!;

    public ICollection<PdmCustomProperty> CustomProperties { get; set; } = new List<PdmCustomProperty>();

    public ICollection<PdmBomOccurrence> ParentBomOccurrences { get; set; } = new List<PdmBomOccurrence>();

    public ICollection<PdmBomOccurrence> ChildBomOccurrences { get; set; } = new List<PdmBomOccurrence>();
}

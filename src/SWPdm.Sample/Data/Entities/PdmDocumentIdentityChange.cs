namespace SWPdm.Sample.Data.Entities;

public sealed class PdmDocumentIdentityChange
{
    public long IdentityChangeId { get; set; }

    public long SourceDocumentId { get; set; }

    public long SourceVersionId { get; set; }

    public long TargetDocumentId { get; set; }

    public string OldPartNumber { get; set; } = string.Empty;

    public string NewPartNumber { get; set; } = string.Empty;

    public string ChangeReason { get; set; } = string.Empty;

    public string ChangedBy { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public PdmDocument SourceDocument { get; set; } = null!;

    public PdmDocumentVersion SourceVersion { get; set; } = null!;

    public PdmDocument TargetDocument { get; set; } = null!;
}

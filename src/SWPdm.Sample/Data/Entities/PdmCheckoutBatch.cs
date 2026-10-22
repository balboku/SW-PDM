namespace SWPdm.Sample.Data.Entities;

// Historical identifiers must survive removal of live documents/versions.
public sealed class PdmCheckoutBatch
{
    public Guid BatchId { get; set; }
    public long RootDocumentId { get; set; }
    public string Actor { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public List<PdmCheckoutBatchItem> Items { get; set; } = [];
}

public sealed class PdmCheckoutBatchItem
{
    public Guid BatchId { get; set; }
    public long DocumentId { get; set; }
    public long VersionId { get; set; }
    public DateTimeOffset LockTime { get; set; }
    public DateTimeOffset? RestoredAt { get; set; }
    public PdmCheckoutBatch Batch { get; set; } = null!;
}

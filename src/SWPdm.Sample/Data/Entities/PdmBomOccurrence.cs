namespace SWPdm.Sample.Data.Entities;

public sealed class PdmBomOccurrence
{
    public long BomOccurrenceId { get; set; }

    public long ParentVersionId { get; set; }

    public long? ChildVersionId { get; set; }

    public string OccurrencePath { get; set; } = string.Empty;

    public string ParentConfigurationName { get; set; } = string.Empty;

    public string ChildConfigurationName { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1m;

    public string? FindNumber { get; set; }

    public string SourceReferencePath { get; set; } = string.Empty;

    public string PackageRelativePath { get; set; } = string.Empty;

    public string ReferenceStatus { get; set; } = "Resolved";

    public bool IsSuppressed { get; set; }

    public bool IsVirtual { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public PdmDocumentVersion ParentVersion { get; set; } = null!;

    public PdmDocumentVersion? ChildVersion { get; set; }
}

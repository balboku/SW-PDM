namespace SWPdm.Sample.Data.Entities;

public sealed class PdmDocument
{
    public long DocumentId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FileExtension { get; set; } = string.Empty;

    public string DocumentType { get; set; } = string.Empty;

    public string? PartNumber { get; set; }

    public string? RevisionLabel { get; set; }

    public string? Material { get; set; }

    public string? Designer { get; set; }

    public long? CurrentVersionId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string? CheckedOutBy { get; set; }

    public DateTimeOffset? CheckedOutAt { get; set; }

    public PdmDocumentVersion? CurrentVersion { get; set; }

    public ICollection<PdmDocumentVersion> Versions { get; set; } = new List<PdmDocumentVersion>();
}

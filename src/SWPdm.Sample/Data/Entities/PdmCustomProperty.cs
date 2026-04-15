namespace SWPdm.Sample.Data.Entities;

public sealed class PdmCustomProperty
{
    public long CustomPropertyId { get; set; }

    public long VersionId { get; set; }

    public string ConfigurationName { get; set; } = string.Empty;

    public string PropertyName { get; set; } = string.Empty;

    public string? PropertyValue { get; set; }

    public string? PropertyType { get; set; }

    public string? RawExpression { get; set; }

    public bool IsResolved { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public PdmDocumentVersion Version { get; set; } = null!;
}

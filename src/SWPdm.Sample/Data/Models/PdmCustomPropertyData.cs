namespace SWPdm.Sample.Data.Models;

public sealed record PdmCustomPropertyData(
    long CustomPropertyId,
    string ConfigurationName,
    string PropertyName,
    string? PropertyValue,
    string? PropertyType,
    string? RawExpression,
    bool IsResolved);

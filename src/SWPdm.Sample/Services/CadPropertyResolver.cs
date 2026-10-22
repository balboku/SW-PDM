namespace SWPdm.Sample.Services;

public sealed record CadPropertyValue(string? Value, string Source, IReadOnlyList<string> Conflicts);

public static class CadPropertyResolver
{
    public static readonly string[] PartNumberNames = ["品號", "PartNumber", "Number", "Part No", "PartNo"];
    public static readonly string[] PartNameNames = ["品名", "PartName", "Description", "描述"];
    public static readonly string[] RevisionNames = ["版次", "Revision", "Rev"];

    // Document-level properties are authoritative. Configuration fallback must agree;
    // never choose the first configuration's identity by enumeration order.
    public static CadPropertyValue Resolve(SolidWorksParseResult parsed, params string[] aliases)
    {
        return Resolve(parsed.DocumentProperties.Select(p => (Configuration: "", Name: p.Key, Value: (string?)p.Value.Value))
            .Concat(parsed.ConfigurationProperties.SelectMany(c => c.Value.Select(p =>
                (Configuration: c.Key, Name: p.Key, Value: (string?)p.Value.Value)))), aliases);
    }

    public static CadPropertyValue Resolve(IEnumerable<(string Configuration, string Name, string? Value)> properties,
        params string[] aliases)
    {
        var candidates = properties.Where(p => aliases.Contains(p.Name, StringComparer.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(p.Value)).ToArray();
        var documentValues = candidates.Where(p => string.IsNullOrEmpty(p.Configuration)).ToArray();
        var selected = (documentValues.Length > 0 ? documentValues : candidates)
            .OrderBy(p => Array.FindIndex(aliases, a => a.Equals(p.Name, StringComparison.OrdinalIgnoreCase)))
            .ThenBy(p => p.Configuration, StringComparer.OrdinalIgnoreCase).ToArray();
        var values = selected.Select(p => p.Value!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new(values.Length == 1 ? values[0] : null,
            string.Join("、", selected.Select(p => $"{(p.Configuration == "" ? "文件" : p.Configuration)}:{p.Name}")),
            values.Length > 1 ? values : []);
    }
}

namespace SWPdm.Api.Contracts;

using SWPdm.Sample.Services;

public sealed record SolidWorksParseResponse(
    string FilePath,
    string DocumentType,
    IReadOnlyDictionary<string, SolidWorksCustomProperty> DocumentProperties,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, SolidWorksCustomProperty>> ConfigurationProperties,
    IReadOnlyList<string> ReferencedFilePaths);

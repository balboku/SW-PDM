namespace SWPdm.Api.Configuration;

public sealed class SolidWorksDocumentManagerOptions
{
    public const string SectionName = "SolidWorksDocumentManager";

    public string LicenseKey { get; set; } = string.Empty;

    public string[] ReferenceSearchPaths { get; set; } = Array.Empty<string>();
}

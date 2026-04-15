namespace SWPdm.Api.Configuration;

public sealed class LocalStorageOptions
{
    public const string SectionName = "LocalStorage";

    public string VaultPath { get; set; } = string.Empty;
}

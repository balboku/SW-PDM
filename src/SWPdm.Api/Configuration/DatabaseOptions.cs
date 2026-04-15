namespace SWPdm.Api.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string Provider { get; set; } = "PostgreSql";

    public string ConnectionString { get; set; } = string.Empty;
}

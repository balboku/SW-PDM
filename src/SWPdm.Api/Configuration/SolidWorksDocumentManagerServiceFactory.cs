namespace SWPdm.Api.Configuration;

using Microsoft.Extensions.Options;
using SWPdm.Sample.Services;

// Narrow ingestion seam for isolated contract tests; the production CAD parser is unchanged.
public interface ICadIngestionSession : IDisposable
{
    SolidWorksParseResult Parse(string filePath, IEnumerable<string>? additionalSearchPaths = null);
}

public interface ICadIngestionSessionFactory
{
    ICadIngestionSession Open();
}

public sealed class SolidWorksDocumentManagerServiceFactory : ICadIngestionSessionFactory
{
    private readonly IOptions<SolidWorksDocumentManagerOptions> _options;

    public SolidWorksDocumentManagerServiceFactory(IOptions<SolidWorksDocumentManagerOptions> options)
    {
        _options = options;
    }

    public SolidWorksDocumentManagerService Create()
    {
        string licenseKey = _options.Value.LicenseKey;

        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            throw new InvalidOperationException("SolidWorksDocumentManager:LicenseKey is not configured.");
        }

        return new SolidWorksDocumentManagerService(licenseKey);
    }

    public ICadIngestionSession Open() => new IngestionSession(Create());

    private sealed class IngestionSession(SolidWorksDocumentManagerService service) : ICadIngestionSession
    {
        public SolidWorksParseResult Parse(string filePath, IEnumerable<string>? additionalSearchPaths = null)
            => service.Parse(filePath, additionalSearchPaths);
        public void Dispose() => service.Dispose();
    }
}

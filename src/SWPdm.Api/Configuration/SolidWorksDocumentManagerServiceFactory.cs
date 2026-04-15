namespace SWPdm.Api.Configuration;

using Microsoft.Extensions.Options;
using SWPdm.Sample.Services;

public sealed class SolidWorksDocumentManagerServiceFactory
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
}

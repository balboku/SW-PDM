namespace SWPdm.Api.Configuration;

using Microsoft.Extensions.Options;
using SWPdm.Sample.Services;

public interface ICadPackageSession : IDisposable
{
    CadPackageInspection Inspect(string copiedFile);
    void Rewrite(string copiedFile, IReadOnlyDictionary<string, string> replacements);
}
public interface ICadPackageSessionFactory { ICadPackageSession Open(string workspace); }

public sealed class CadPackageSessionFactory(IOptions<SolidWorksDocumentManagerOptions> options) : ICadPackageSessionFactory
{
    public ICadPackageSession Open(string workspace) => new Session(new(options.Value.LicenseKey, workspace));
    private sealed class Session(SolidWorksPackageSession native) : ICadPackageSession
    {
        public CadPackageInspection Inspect(string copiedFile) => native.Inspect(copiedFile);
        public void Rewrite(string copiedFile, IReadOnlyDictionary<string, string> replacements) => native.Rewrite(copiedFile, replacements);
        public void Dispose() => native.Dispose();
    }
}

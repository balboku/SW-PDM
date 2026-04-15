namespace SWPdm.Sample.Data.Repositories;

using SWPdm.Sample.Data.Models;

public interface IPdmRepository
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);

    Task<PdmDocumentDetails?> GetDocumentAsync(long documentId, CancellationToken cancellationToken = default);

    Task<PdmVersionDetails?> GetVersionAsync(long versionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PdmBomLinkData>> GetImmediateChildrenAsync(long parentVersionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PdmPackageFile>> GetPackageClosureAsync(long rootVersionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PdmPackageFile>> GetWhereUsedAsync(long childVersionId, CancellationToken cancellationToken = default);
}

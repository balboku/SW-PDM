namespace SWPdm.Sample.Data.Models;

public sealed record PdmBomLinkData(
    long BomOccurrenceId,
    long ParentVersionId,
    long? ChildVersionId,
    string OccurrencePath,
    string ParentConfigurationName,
    string ChildConfigurationName,
    decimal Quantity,
    string? FindNumber,
    string SourceReferencePath,
    string PackageRelativePath,
    string ReferenceStatus,
    bool IsSuppressed,
    bool IsVirtual,
    string? ChildDocumentType,
    string? ChildOriginalFileName,
    string? ChildStorageFileId);

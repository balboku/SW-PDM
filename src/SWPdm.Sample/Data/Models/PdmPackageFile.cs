namespace SWPdm.Sample.Data.Models;

public sealed record PdmPackageFile(
    long VersionId,
    string DocumentType,
    string StorageFileId,
    string OriginalFileName,
    string SourceFilePath,
    string VaultRelativePath,
    int Depth);

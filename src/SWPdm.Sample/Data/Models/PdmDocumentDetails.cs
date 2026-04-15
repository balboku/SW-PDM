namespace SWPdm.Sample.Data.Models;

public sealed record PdmDocumentDetails(
    long DocumentId,
    string FileName,
    string FileExtension,
    string DocumentType,
    string? PartNumber,
    string? RevisionLabel,
    string? Material,
    string? Designer,
    long? CurrentVersionId,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<PdmDocumentVersionSummary> Versions);

namespace SWPdm.Api.Contracts;

public sealed record IngestCadFileResponse(
    long RootDocumentId,
    long RootVersionId,
    string RootDocumentType,
    string RootStorageFileId,
    int ProcessedFileCount,
    IReadOnlyList<IngestedFileResponse> Files,
    IReadOnlyList<string> Issues);

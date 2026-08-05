namespace SWPdm.Api.Contracts;

public sealed record PartNumberChangeResponse(
    long IdentityChangeId,
    long SourceDocumentId,
    long SourceVersionId,
    long TargetDocumentId,
    string OldPartNumber,
    string NewPartNumber,
    string ChangeReason,
    string ChangedBy,
    DateTimeOffset CreatedAt);

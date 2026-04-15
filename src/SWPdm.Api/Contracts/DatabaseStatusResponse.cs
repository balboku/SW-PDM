namespace SWPdm.Api.Contracts;

public sealed record DatabaseStatusResponse(
    string Provider,
    bool IsConfigured,
    bool CanConnect,
    string? ErrorMessage);

namespace SWPdm.Api.Contracts;

public sealed record ConfigurationStatusResponse(
    bool IsDatabaseConfigured,
    string DatabaseProvider,
    bool IsGoogleDriveConfigured,
    bool IsSolidWorksDocumentManagerConfigured,
    string DefaultGoogleDriveFolderId,
    IReadOnlyList<string> SolidWorksReferenceSearchPaths);

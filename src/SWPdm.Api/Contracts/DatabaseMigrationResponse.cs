namespace SWPdm.Api.Contracts;

public sealed record DatabaseMigrationResponse(
    bool Applied,
    int PendingMigrationCountBefore,
    int PendingMigrationCountAfter,
    IReadOnlyList<string> AppliedMigrations);

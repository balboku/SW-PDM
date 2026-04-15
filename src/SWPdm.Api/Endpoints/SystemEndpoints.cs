using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SWPdm.Api.Configuration;
using SWPdm.Api.Contracts;
using SWPdm.Sample.Data;
using SWPdm.Sample.Data.Repositories;

namespace SWPdm.Api.Endpoints;

public static class SystemEndpoints
{
    public static void MapSystemEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions());

        app.MapGet("/api/config/status", (
            IOptions<DatabaseOptions> databaseOptions,
            IOptions<LocalStorageOptions> localStorageOptions,
            IOptions<SolidWorksDocumentManagerOptions> solidWorksOptions,
            IConfiguration configuration) =>
        {
            DatabaseOptions database = databaseOptions.Value;
            LocalStorageOptions storage = localStorageOptions.Value;
            SolidWorksDocumentManagerOptions solidWorks = solidWorksOptions.Value;

            return Results.Ok(new
            {
                IsDatabaseConfigured = !string.IsNullOrWhiteSpace(database.ConnectionString)
                    || !string.IsNullOrWhiteSpace(configuration.GetConnectionString("Pdm")),
                DatabaseProvider = database.Provider,
                IsLocalStorageConfigured = !string.IsNullOrWhiteSpace(storage.VaultPath),
                LocalStorageVaultPath = storage.VaultPath,
                IsSolidWorksDocumentManagerConfigured = !string.IsNullOrWhiteSpace(solidWorks.LicenseKey),
                SolidWorksReferenceSearchPaths = solidWorks.ReferenceSearchPaths
            });
        });

        app.MapGet("/api/database/status", async (
            IOptions<DatabaseOptions> databaseOptions,
            IPdmRepository repository,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            bool isConfigured = !string.IsNullOrWhiteSpace(databaseOptions.Value.ConnectionString)
                || !string.IsNullOrWhiteSpace(configuration.GetConnectionString("Pdm"));

            if (!isConfigured)
            {
                return Results.Ok(new DatabaseStatusResponse(
                    Provider: databaseOptions.Value.Provider,
                    IsConfigured: false,
                    CanConnect: false,
                    ErrorMessage: "Database connection string is not configured."));
            }

            try
            {
                bool canConnect = await repository.CanConnectAsync(cancellationToken);

                return Results.Ok(new DatabaseStatusResponse(
                    Provider: databaseOptions.Value.Provider,
                    IsConfigured: true,
                    CanConnect: canConnect,
                    ErrorMessage: canConnect ? null : "Database provider was configured, but the connection test failed."));
            }
            catch (Exception ex)
            {
                return Results.Ok(new DatabaseStatusResponse(
                    Provider: databaseOptions.Value.Provider,
                    IsConfigured: true,
                    CanConnect: false,
                    ErrorMessage: ex.Message));
            }
        });

        app.MapPost("/api/database/migrate", async (
            PdmDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            try
            {
                string[] pendingBefore = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

                await dbContext.Database.MigrateAsync(cancellationToken);

                string[] pendingAfter = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

                return Results.Ok(new DatabaseMigrationResponse(
                    Applied: pendingBefore.Length > 0,
                    PendingMigrationCountBefore: pendingBefore.Length,
                    PendingMigrationCountAfter: pendingAfter.Length,
                    AppliedMigrations: pendingBefore));
            }
            catch (Exception ex)
            {
                // This is a minimal API pattern ToProblem from original code. 
                // We recreate it inline or use Results.Problem directly to avoid copy/paste of static method.
                return Results.Problem(
                    title: "Unexpected error",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });
    }
}

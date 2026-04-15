using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SWPdm.Api.Configuration;
using SWPdm.Api.Endpoints;
using SWPdm.Api.Services;
using SWPdm.Sample.Data;
using SWPdm.Sample.Data.Repositories;
using SWPdm.Sample.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

builder.Services
    .AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebFrontend",
        policy => policy
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services
    .AddOptions<LocalStorageOptions>()
    .Bind(builder.Configuration.GetSection(LocalStorageOptions.SectionName));

builder.Services
    .AddOptions<SolidWorksDocumentManagerOptions>()
    .Bind(builder.Configuration.GetSection(SolidWorksDocumentManagerOptions.SectionName));

builder.Services.AddDbContext<PdmDbContext>((serviceProvider, options) =>
{
    DatabaseOptions databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

    string provider = string.IsNullOrWhiteSpace(databaseOptions.Provider)
        ? "PostgreSql"
        : databaseOptions.Provider;

    string connectionString = !string.IsNullOrWhiteSpace(databaseOptions.ConnectionString)
        ? databaseOptions.ConnectionString
        : builder.Configuration.GetConnectionString("Pdm") ?? string.Empty;

    if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlServer(connectionString);
        return;
    }

    options.UseNpgsql(connectionString);
});

builder.Services.AddScoped<IPdmRepository, PdmRepository>();
builder.Services.AddScoped<PdmIngestionService>();
builder.Services.AddSingleton<LocalStorageService>(sp =>
{
    var options = sp.GetRequiredService<IOptions<LocalStorageOptions>>().Value;
    return new LocalStorageService(options.VaultPath, sp.GetRequiredService<ILogger<LocalStorageService>>());
});
builder.Services.AddSingleton<SolidWorksDocumentManagerServiceFactory>();

var app = builder.Build();

app.UseCors("AllowWebFrontend");

app.MapGet("/", () => Results.Ok(new
{
    service = "SWPdm.Api",
    status = "running",
    endpoints = new[]
    {
        "GET /health",
        "GET /api/config/status",
        "GET /api/database/status",
        "POST /api/database/migrate",
        "GET /api/documents/search",
        "GET /api/documents/{documentId}",
        "GET /api/versions/{versionId}",
        "GET /api/versions/{versionId}/children",
        "GET /api/assemblies/{rootVersionId}/package-closure",
        "GET /api/assemblies/{rootVersionId}/download-zip",
        "POST /api/web/upload-temp",
        "POST /api/ingest/cad",
        "POST /api/storage/upload",
        "POST /api/storage/download",
        "POST /api/solidworks/parse"
    }
}));

// Set up endpoints module routing
app.MapSystemEndpoints();
app.MapDocumentEndpoints();
app.MapIngestEndpoints();

if (!EF.IsDesignTime)
{
    app.Run();
}

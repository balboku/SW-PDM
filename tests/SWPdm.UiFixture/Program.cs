using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SWPdm.Api.Configuration;
using SWPdm.Api.Endpoints;
using SWPdm.Api.Services;
using SWPdm.Sample.Data;
using SWPdm.Sample.Data.Entities;
using SWPdm.Sample.Data.Repositories;
using SWPdm.Sample.Services;

// Disposable, synthetic UI fixture. Never loads a production connection string or vault.
var builder = WebApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.WebHost.UseUrls("http://127.0.0.1:5010");
var connection = new SqliteConnection($"Data Source=swpdm_ui_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
connection.Open();
builder.Services.AddDbContext<PdmDbContext>(o => o.UseSqlite(connection.ConnectionString).ReplaceService<IModelCustomizer, FixtureModelCustomizer>());
builder.Services.AddScoped<IPdmRepository, PdmRepository>();
builder.Services.AddScoped<CancelCheckoutService>();
builder.Services.AddScoped<PdmIngestionService>();
builder.Services.Configure<LocalStorageOptions>(_ => { });
builder.Services.AddSingleton<ICadIngestionSessionFactory, UiCadParser>();
builder.Services.AddSingleton<SolidWorksDocumentManagerServiceFactory>();
builder.Services.Configure<SolidWorksDocumentManagerOptions>(_ => { });
string fixtureFolder = Path.Combine(Path.GetTempPath(), "swpdm_ui_fixture_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixtureFolder);
string noopUploadPath = Path.Combine(fixtureFolder, "TEST-058_試用回饋驗證圖面.sldprt");
File.WriteAllText(noopUploadPath, "contract-ui-noop");
builder.Services.AddSingleton(new LocalStorageService(fixtureFolder));
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins("http://127.0.0.1:5175", "http://localhost:5175").AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Content-Disposition")));
var app = builder.Build();
app.UseCors();
// Fault injection is confined to this disposable fixture, never the product API.
string scenario = "normal";
app.Use(async (context, next) =>
{
    string path = context.Request.Path.Value ?? "";
    bool fail = (scenario == "fail-config" && path == "/api/config/status")
        || (scenario == "fail-search" && path == "/api/documents/search")
        || (scenario == "fail-detail" && path == "/api/documents/57")
        || (scenario == "fail-relations" && path.EndsWith("/checkout-references"))
        || (scenario == "fail-checkout" && path.EndsWith("/checkout"))
        || (scenario == "fail-bom" && path.EndsWith("/children"))
        || (scenario == "fail-cancel-preview" && path.EndsWith("/undo-checkout-preview"));
    if (fail)
    {
        await Results.Problem(statusCode: 503, title: "合成驗證失敗", detail: "這是隔離 UI fixture 的預期錯誤；未更動正式資料。").ExecuteAsync(context);
        return;
    }
    if (scenario == "stale-cancel" && path.EndsWith("/undo-checkout") && context.Request.Method == "POST")
    {
        var db = context.RequestServices.GetRequiredService<PdmDbContext>();
        var document = await db.Documents.FindAsync(58L);
        document!.CheckedOutAt = document.CheckedOutAt!.Value.AddSeconds(1);
        await db.SaveChangesAsync();
    }
    if (scenario == "empty" && path == "/api/documents/search")
    {
        await Results.Ok(new { items = Array.Empty<object>(), page = 1, pageSize = 50, totalCount = 0, totalPages = 1,
            missingReferenceDocumentCount = 0, checkedOutCount = 0, newerReferenceDocumentCount = 0 }).ExecuteAsync(context);
        return;
    }
    if (scenario == "conflict" && path.EndsWith("/checkout-references"))
    {
        var blocked = new { documentId = 58, versionId = 58, documentType = "Part", originalFileName = "TEST-058_他人鎖定.sldprt", checkedOutBy = "OtherTester" };
        await Results.Ok(new { references = new[] { blocked, blocked }, drawings = Array.Empty<object>(), whereUsed = Array.Empty<object>() }).ExecuteAsync(context);
        return;
    }
    if (path == "/api/versions/56/download")
    {
        await Results.File(System.Text.Encoding.UTF8.GetBytes("Synthetic UI download; this is not CAD."), "text/plain", "UI-QC.txt").ExecuteAsync(context);
        return;
    }
    if (path == "/api/documents/search")
    {
        // The published query uses PostgreSQL ILike and DateTimeOffset ordering,
        // neither supported by SQLite. Adapt only the fixture, preserving its array contract.
        var db = context.RequestServices.GetRequiredService<PdmDbContext>();
        var documents = await db.Documents.Include(d => d.CurrentVersion).ThenInclude(v => v!.CustomProperties).ToListAsync();
        string query = context.Request.Query["query"].ToString();
        string type = context.Request.Query["documentType"].ToString();
        string status = context.Request.Query["status"].ToString();
        string? DrawingNumber(PdmDocument d) => d.CurrentVersion?.CustomProperties.FirstOrDefault(p => p.PropertyName == "圖號")?.PropertyValue;
        var result = documents
            .Where(d => string.IsNullOrWhiteSpace(query) || new[] { d.FileName, d.PartNumber, DrawingNumber(d) }.Any(s => s?.Contains(query, StringComparison.OrdinalIgnoreCase) == true))
            .Where(d => string.IsNullOrWhiteSpace(type) || type == "All" || d.DocumentType == type)
            .Where(d => status == "CheckedOut" ? d.CheckedOutBy != null : status != "Available" || d.CheckedOutBy == null)
            .OrderByDescending(d => d.UpdatedAt).Take(50)
            .Select(d => new { d.DocumentId, d.FileName, DrawingNumber = DrawingNumber(d), d.PartNumber, d.DocumentType,
                d.RevisionLabel, d.Material, CurrentVersionNo = d.CurrentVersion?.VersionNo,
                d.CurrentVersionId, CurrentLifecycleState = d.CurrentVersion?.LifecycleState,
                ReferenceUpdateCount = 0, AffectedReferenceOccurrenceCount = 0, d.UpdatedAt, d.CheckedOutBy, d.CheckedOutAt });
        await Results.Ok(result).ExecuteAsync(context);
        return;
    }
    await next();
});
app.MapPost("/fixture/scenario/{name}", (string name) =>
{
    if (!new[] { "normal", "empty", "fail-config", "fail-search", "fail-detail", "fail-relations", "fail-checkout", "fail-bom", "conflict", "fail-cancel-preview", "stale-cancel" }.Contains(name))
        return Results.BadRequest();
    scenario = name;
    return Results.Ok(new { fixture = true, scenario });
});
app.MapGet("/api/config/status", () => Results.Ok(new { isDatabaseConfigured = true, databaseProvider = "SQLite (isolated UI fixture)",
    isLocalStorageConfigured = true, localStorageVaultPath = fixtureFolder, isSolidWorksDocumentManagerConfigured = false }));
app.MapGet("/api/database/status", () => Results.Ok(new { isConfigured = true, canConnect = true }));
app.MapDocumentEndpoints();
app.MapIngestEndpoints();
app.MapGet("/health", () => Results.Ok(new { fixture = true, scenario, noopUploadPath }));
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PdmDbContext>();
    db.Database.EnsureCreated();
    for (int i = 1; i <= 60; i++)
    {
        bool drawing = i == 59;
        bool assembly = i == 60;
        string extension = drawing ? ".slddrw" : assembly ? ".sldasm" : ".sldprt";
        var d = new PdmDocument { FileName = i == 55 ? "TEST-055_長檔名與長圖號手機換行驗證_" + new string('X', 90) : $"TEST-{i:000}_試用回饋驗證圖面", PartNumber = $"P-{i:000}",
            DocumentType = drawing ? "Drawing" : assembly ? "Assembly" : "Part", FileExtension = extension,
            RevisionLabel = "A", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        db.Documents.Add(d); db.SaveChanges();
        var v = new PdmDocumentVersion { DocumentId = d.DocumentId, VersionNo = 1, RevisionLabel = "A", LifecycleState = "WIP",
            OriginalFileName = d.FileName + extension, StorageFileId = "fixture-not-a-real-cad", SourceFilePath = d.FileName + extension,
            VaultRelativePath = "fixture", CreatedAt = DateTimeOffset.UtcNow,
            ChecksumSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("contract-ui-noop"))).ToLowerInvariant() };
        db.DocumentVersions.Add(v); db.SaveChanges();
        d.CurrentVersionId = v.VersionId;
        db.CustomProperties.AddRange(
            new PdmCustomProperty { VersionId = v.VersionId, PropertyName = "品名", PropertyValue = $"驗證用零組件 {i}", PropertyType = "Text" },
            new PdmCustomProperty { VersionId = v.VersionId, PropertyName = "品號", PropertyValue = d.PartNumber, PropertyType = "Text" },
            new PdmCustomProperty { VersionId = v.VersionId, PropertyName = "圖號", PropertyValue = $"DWG-{i:000}" + (i == 55 ? "-" + new string('N', 70) : ""), PropertyType = "Text" },
            new PdmCustomProperty { VersionId = v.VersionId, PropertyName = "版次", PropertyValue = "A", PropertyType = "Text" });
        db.SaveChanges();
    }
    db.BomOccurrences.AddRange(
        new PdmBomOccurrence { ParentVersionId = 59, ChildVersionId = 58, SourceReferencePath = "TEST-058.sldprt", OccurrencePath = "1", PackageRelativePath = "TEST-058.sldprt" },
        new PdmBomOccurrence { ParentVersionId = 60, ChildVersionId = 58, SourceReferencePath = "TEST-058.sldprt", OccurrencePath = "1", PackageRelativePath = "TEST-058.sldprt" },
        new PdmBomOccurrence { ParentVersionId = 60, ReferenceStatus = "Missing", SourceReferencePath = "missing.sldprt", OccurrencePath = "2", PackageRelativePath = "missing.sldprt" });
    db.SaveChanges();
    foreach (var document in db.Documents.Where(d => d.DocumentId == 58 || d.DocumentId == 59 || d.DocumentId == 60))
    {
        document.CheckedOutBy = "User";
        document.CheckedOutAt = DateTimeOffset.UtcNow;
    }
    db.SaveChanges();
}
app.Lifetime.ApplicationStopped.Register(() => { connection.Dispose(); Directory.Delete(fixtureFolder, true); });
app.Run();

// Test-only storage mapping lets unchanged published endpoints order timestamps in SQLite.
public sealed class FixtureModelCustomizer(ModelCustomizerDependencies dependencies) : RelationalModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetProperties()))
            if ((Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType) == typeof(DateTimeOffset))
                property.SetValueConverter(new ValueConverter<DateTimeOffset, long>(d => d.UtcTicks, ticks => new DateTimeOffset(ticks, TimeSpan.Zero)));
    }
}

// Synthetic parse result only for exercising the real ingestion HTTP/DB contract, not CAD validation.
public sealed class UiCadParser : ICadIngestionSessionFactory, ICadIngestionSession
{
    public ICadIngestionSession Open() => this;
    public SolidWorksParseResult Parse(string filePath, IEnumerable<string>? additionalSearchPaths = null) => new(
        filePath, SolidWorksDocumentKind.Part,
        new Dictionary<string, SolidWorksCustomProperty> {
            ["PartNumber"] = new("PartNumber", "P-058", "Text"), ["Revision"] = new("Revision", "A", "Text") },
        new Dictionary<string, IReadOnlyDictionary<string, SolidWorksCustomProperty>>(), Array.Empty<string>(), null);
    public void Dispose() { }
}

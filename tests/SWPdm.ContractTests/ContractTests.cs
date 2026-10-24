using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using SWPdm.Api.Configuration;
using SWPdm.Api.Endpoints;
using SWPdm.Api.Services;
using SWPdm.Sample.Data;
using SWPdm.Sample.Data.Entities;
using SWPdm.Sample.Data.Repositories;
using SWPdm.Sample.Services;
using Xunit;

namespace SWPdm.ContractTests;

public sealed class ContractTests
{
    [Fact]
    public async Task CancelPreviewAndAtomicPostOnlyUnlockSelectedDocument()
    {
        await using var host = await ContractHost.Start();
        var preview = await host.Preview();
        Assert.True(preview.GetProperty("canRestore").GetBoolean());
        Assert.Equal("SingleDocument", preview.GetProperty("scope").GetString());
        var result = await host.Client.PostAsJsonAsync("/api/documents/1/undo-checkout", host.Request(preview));
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(1, (await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("restoredCount").GetInt32());
        var state = await host.Snapshot();
        Assert.Null(state.Owner);
        Assert.Equal("User", state.OtherOwner);
        Assert.Equal(1, state.Versions);
        Assert.Equal(1, state.BomRows);
        Assert.Equal(host.LockTime, state.OtherLock);
        Assert.Equal(host.UpdatedAt, state.UpdatedAt);
        var replay = await host.Client.PostAsJsonAsync("/api/documents/1/undo-checkout", host.Request(preview));
        await AssertConflict(replay);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("time")]
    [InlineData("version")]
    public async Task ChangesAfterPreviewCannotUnlock(string change)
    {
        await using var host = await ContractHost.Start();
        var preview = await host.Preview();
        await host.WithDb(async db => {
            var d = await db.Documents.FindAsync(1L);
            if (change == "owner") d!.CheckedOutBy = "Other";
            if (change == "time") d!.CheckedOutAt = host.LockTime.AddTicks(10);
            if (change == "version") d!.CurrentVersionId = null;
            await db.SaveChangesAsync();
        });
        var before = await host.Snapshot();
        await AssertConflict(await host.Client.PostAsJsonAsync("/api/documents/1/undo-checkout", host.Request(preview)));
        Assert.Equal(before, await host.Snapshot());
    }

    [Theory]
    [InlineData("/api/documents/1/undo-checkout")]
    [InlineData("/api/documents/1/checkin")]
    public async Task MissingBodyFieldsAndBatchRequestsFailClosed(string route)
    {
        await using var host = await ContractHost.Start();
        var before = await host.Snapshot();
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsync(route, null)).StatusCode);
        foreach (var payload in new object[] { new { actor = "User" }, new { actor = "" },
            new { actor = "User", expectedLockTime = host.LockTime, expectedVersionId = 1, includeBatch = true },
            new { actor = "User", expectedLockTime = host.LockTime, expectedVersionId = 1, batchId = Guid.NewGuid() } })
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync(route, payload)).StatusCode);
        Assert.Equal(before, await host.Snapshot());
    }

    [Fact]
    public async Task OtherActorNoLockAndMissingDocumentAreVisibleInPreview()
    {
        await using var host = await ContractHost.Start();
        Assert.False((await host.Preview("Other")).GetProperty("canRestore").GetBoolean());
        var correct = await host.Preview();
        var request = host.Request(correct, "Other");
        await AssertConflict(await host.Client.PostAsJsonAsync("/api/documents/1/undo-checkout", request));
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/api/documents/999/undo-checkout-preview?actor=User")).StatusCode);
        await host.Client.PostAsJsonAsync("/api/documents/1/undo-checkout", host.Request(correct));
        var empty = await host.Preview();
        Assert.False(empty.GetProperty("canRestore").GetBoolean());
        Assert.Contains("不需要再取消", empty.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task ConcurrentConfirmationsHaveExactlyOneWinner()
    {
        await using var host = await ContractHost.Start();
        var preview = await host.Preview();
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            host.Client.PostAsJsonAsync("/api/documents/1/undo-checkout", host.Request(preview))));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(7, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal("User", (await host.Snapshot()).OtherOwner);
    }

    [Fact]
    public async Task ReusedIsRealIngestionResponseWithoutAnyDatabaseOrVaultMutation()
    {
        await using var host = await ContractHost.Start();
        var before = await host.Snapshot();
        var response = await host.Ingest();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Reused", body.GetProperty("rootOutcome").GetString());
        Assert.Equal("Reused", body.GetProperty("files")[0].GetProperty("outcome").GetString());
        Assert.Equal(1, body.GetProperty("rootVersionId").GetInt64());
        Assert.Equal(before, await host.Snapshot());
        Assert.Empty(Directory.EnumerateFiles(host.Vault, "*", SearchOption.AllDirectories));
        Assert.Equal(1, host.Parser.ParseCount); // Root no-op must not recurse into references.
        Assert.Equal(host.Checksum, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(host.FilePath))).ToLowerInvariant());
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("identity")]
    [InlineData("filename")]
    public async Task ReusedDoesNotBypassOwnerIdentityOrRenamePolicies(string invalid)
    {
        await using var host = await ContractHost.Start();
        var before = await host.Snapshot();
        if (invalid == "identity") host.Parser.PartNumber = "WRONG";
        if (invalid == "filename") File.Move(host.FilePath, host.FilePath = Path.Combine(host.Folder, "renamed.sldprt"));
        var response = await host.Ingest(invalid == "actor" ? "Other" : "User");
        if (invalid == "filename")
        {
            // DEV-012 intentionally permits an explicit WIP rename with owner + reason.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("CreatedVersion", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rootOutcome").GetString());
            Assert.Equal(before.Versions + 1, (await host.Snapshot()).Versions);
            Assert.Equal(before.OtherOwner, (await host.Snapshot()).OtherOwner);
            return;
        }
        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal(before, await host.Snapshot());
        Assert.Empty(Directory.EnumerateFiles(host.Vault, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ChangedContentStillCreatesVersionUnderExistingRevisionPolicy()
    {
        await using var host = await ContractHost.Start();
        await File.WriteAllTextAsync(host.FilePath, "changed synthetic CAD content");
        host.Parser.Revision = "B";
        var response = await host.Ingest();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CreatedVersion", body.GetProperty("rootOutcome").GetString());
        Assert.Equal("CreatedVersion", body.GetProperty("files")[0].GetProperty("outcome").GetString());
        Assert.Equal(2, (await host.Snapshot()).Versions);
        Assert.Null((await host.Snapshot()).Owner);
    }

    private static async Task AssertConflict(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("LOCK_CHANGED", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }
}

internal sealed record State(string? Owner, DateTimeOffset? Lock, string? OtherOwner,
    DateTimeOffset? OtherLock, DateTimeOffset UpdatedAt, int Versions, int BomRows, long? MissingChild);

internal sealed class ContractHost : IAsyncDisposable
{
    public string Folder { get; } = Path.Combine(Path.GetTempPath(), "swpdm_contract_" + Guid.NewGuid().ToString("N"));
    public string Vault => Path.Combine(Folder, "vault");
    public string FilePath { get; set; } = "";
    public string Checksum { get; private set; } = "";
    public DateTimeOffset LockTime { get; } = new DateTimeOffset(2026, 10, 22, 1, 2, 3, TimeSpan.Zero).AddTicks(1234560);
    public DateTimeOffset UpdatedAt { get; } = new(2026, 10, 21, 0, 0, 0, TimeSpan.Zero);
    public FakeParser Parser { get; } = new();
    public FakePackageCad PackageCad { get; } = new();
    public HttpClient Client { get; private set; } = null!;
    private WebApplication app = null!;
    private SqliteConnection? sqlite;
    private string? postgresAdmin;
    private string? database;

    public static async Task<ContractHost> Start(Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor? interceptor = null)
    {
        var host = new ContractHost();
        Directory.CreateDirectory(host.Folder); Directory.CreateDirectory(host.Vault);
        host.FilePath = Path.Combine(host.Folder, $"{Guid.NewGuid()}_original.sldprt");
        await File.WriteAllTextAsync(host.FilePath, "synthetic CAD contract fixture; not a real SolidWorks file");
        host.Checksum = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(host.FilePath))).ToLowerInvariant();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", ContentRootPath = AppContext.BaseDirectory });
        builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        string? pg = Environment.GetEnvironmentVariable("SWPDM_CONTRACT_PG");
        if (!string.IsNullOrWhiteSpace(pg))
        {
            var cs = new NpgsqlConnectionStringBuilder(pg);
            if (cs.Host != "127.0.0.1" || cs.Port != 55433 || cs.Database != "postgres" || cs.Username != "contract")
                throw new InvalidOperationException("Only the disposable 127.0.0.1:55433 contract cluster is allowed.");
            host.postgresAdmin = cs.ConnectionString;
            host.database = "swpdm_contract_" + Guid.NewGuid().ToString("N");
            await using (var admin = new NpgsqlConnection(host.postgresAdmin)) {
                await admin.OpenAsync(); await using var command = new NpgsqlCommand($"CREATE DATABASE {host.database}", admin); await command.ExecuteNonQueryAsync();
            }
            cs.Database = host.database;
            builder.Services.AddDbContext<PdmDbContext>(o => { o.UseNpgsql(cs.ConnectionString); if (interceptor != null) o.AddInterceptors(interceptor); });
        }
        else
        {
            host.sqlite = new SqliteConnection($"Data Source=contract_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            await host.sqlite.OpenAsync();
            builder.Services.AddDbContext<PdmDbContext>(o => { o.UseSqlite(host.sqlite.ConnectionString).ReplaceService<IModelCustomizer, ContractModelCustomizer>(); if (interceptor != null) o.AddInterceptors(interceptor); });
        }
        builder.Services.AddScoped<IPdmRepository, PdmRepository>();
        builder.Services.AddScoped<CancelCheckoutService>(); builder.Services.AddScoped<PdmIngestionService>();
        builder.Services.AddScoped<CheckoutBatchService>();
        builder.Services.AddScoped<CadPackageService>();
        builder.Services.AddSingleton<ICadPackageSessionFactory>(host.PackageCad);
        builder.Services.Configure<LocalStorageOptions>(_ => { });
        builder.Services.Configure<SolidWorksDocumentManagerOptions>(_ => { });
        builder.Services.AddSingleton<SolidWorksDocumentManagerServiceFactory>();
        builder.Services.AddSingleton<ICadIngestionSessionFactory>(host.Parser);
        builder.Services.AddSingleton(new LocalStorageService(host.Vault));
        host.app = builder.Build(); host.app.MapDocumentEndpoints(); host.app.MapIngestEndpoints();
        await host.WithDb(async db => {
            await db.Database.EnsureCreatedAsync(); // Only synthetic memory DB / guarded disposable cluster.
            var doc = new PdmDocument { FileName = "original", FileExtension = ".sldprt", DocumentType = "Part", PartNumber = "CONTRACT", RevisionLabel = "A", CheckedOutBy = "User", CheckedOutAt = host.LockTime, CreatedAt = host.UpdatedAt, UpdatedAt = host.UpdatedAt };
            var other = new PdmDocument { FileName = "related", FileExtension = ".sldprt", DocumentType = "Part", PartNumber = "RELATED", CheckedOutBy = "User", CheckedOutAt = host.LockTime, CreatedAt = host.UpdatedAt, UpdatedAt = host.UpdatedAt };
            db.AddRange(doc, other); await db.SaveChangesAsync();
            var version = new PdmDocumentVersion { DocumentId = doc.DocumentId, VersionNo = 1, RevisionLabel = "A", OriginalFileName = "original.sldprt", StorageFileId = "old-storage", ChecksumSha256 = host.Checksum, CreatedAt = host.UpdatedAt };
            db.Add(version); await db.SaveChangesAsync(); doc.CurrentVersionId = version.VersionId;
            // This row would be healed by legacy ingestion. A Reused no-op must leave it untouched.
            db.BomOccurrences.Add(new PdmBomOccurrence { ParentVersionId = version.VersionId, SourceReferencePath = "original.sldprt", OccurrencePath = "historical", PackageRelativePath = "original.sldprt", ReferenceStatus = "Missing" });
            await db.SaveChangesAsync();
        });
        await host.app.StartAsync(); host.Client = host.app.GetTestClient(); return host;
    }

    public async Task WithDb(Func<PdmDbContext, Task> action) { using var scope = app.Services.CreateScope(); await action(scope.ServiceProvider.GetRequiredService<PdmDbContext>()); }
    public async Task<State> Snapshot() {
        State result = null!;
        await WithDb(async db => { var d = await db.Documents.SingleAsync(d => d.DocumentId == 1); var other = await db.Documents.SingleAsync(d => d.DocumentId == 2);
            result = new(d.CheckedOutBy, d.CheckedOutAt, other.CheckedOutBy, other.CheckedOutAt, d.UpdatedAt,
                await db.DocumentVersions.CountAsync(), await db.BomOccurrences.CountAsync(), (await db.BomOccurrences.OrderBy(b => b.BomOccurrenceId).FirstAsync()).ChildVersionId); });
        return result;
    }
    public async Task<JsonElement> Preview(string actor = "User") => await Client.GetFromJsonAsync<JsonElement>("/api/documents/1/undo-checkout-preview?actor=" + Uri.EscapeDataString(actor));
    public object Request(JsonElement preview, string? actor = null) => new { actor = actor ?? preview.GetProperty("actor").GetString(), expectedLockTime = preview.GetProperty("checkedOutAt").GetString(), expectedVersionId = preview.GetProperty("currentVersionId").GetInt64(), includeBatch = false };
    public Task<HttpResponseMessage> Ingest(string actor = "User") => Client.PostAsJsonAsync("/api/ingest/cad", new { localFilePath = FilePath, targetDocumentId = 1, uploadedBy = actor, changeReason = "contract verification", ingestReferencedFiles = true });
    public async ValueTask DisposeAsync() {
        Client.Dispose(); await app.DisposeAsync(); if (sqlite is not null) await sqlite.DisposeAsync();
        if (postgresAdmin is not null) { NpgsqlConnection.ClearAllPools(); await using var admin = new NpgsqlConnection(postgresAdmin); await admin.OpenAsync(); await using var command = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin); await command.ExecuteNonQueryAsync(); }
        Directory.Delete(Folder, true); // Exact GUID folder created and owned by this fixture.
    }
}

internal sealed class FakeParser : ICadIngestionSessionFactory, ICadIngestionSession
{
    public Dictionary<string, SolidWorksParseResult> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Failures { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string PartNumber { get; set; } = "CONTRACT";
    public string Revision { get; set; } = "A";
    public int ParseCount { get; private set; }
    public ICadIngestionSession Open() => this;
    public SolidWorksParseResult Parse(string filePath, IEnumerable<string>? additionalSearchPaths = null) {
        ParseCount++;
        if (Failures.Contains(Path.GetFileName(filePath))) throw new InvalidOperationException("合成測試：CAD 無法解析");
        if (Files.TryGetValue(filePath, out var parsed)) return parsed;
        var named = Files.Values.Where(f => Path.GetFileName(f.FilePath) == Path.GetFileName(filePath)).ToArray();
        if (named.Length == 1) return named[0] with { FilePath = filePath };
        return new(filePath, SolidWorksDocumentKind.Part,
            new Dictionary<string, SolidWorksCustomProperty> { ["PartNumber"] = new("PartNumber", PartNumber, "Text"), ["Revision"] = new("Revision", Revision, "Text") },
            new Dictionary<string, IReadOnlyDictionary<string, SolidWorksCustomProperty>>(), Array.Empty<string>(), null);
    }
    public void Dispose() { }
}

public sealed class ContractModelCustomizer(ModelCustomizerDependencies dependencies) : RelationalModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context) {
        base.Customize(modelBuilder, context);
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetProperties()))
            if ((Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType) == typeof(DateTimeOffset))
                property.SetValueConverter(new ValueConverter<DateTimeOffset, long>(d => d.UtcTicks, ticks => new DateTimeOffset(ticks, TimeSpan.Zero)));
    }
}

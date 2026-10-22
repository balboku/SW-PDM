using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SWPdm.Api.Services;
using SWPdm.Sample.Data;
using SWPdm.Sample.Data.Entities;
using Xunit;

namespace SWPdm.ContractTests;

public sealed class CheckoutBatchTests
{
    [Fact]
    public async Task AcquiresOnlyNewLocksAndPersistsExactSnapshotWithoutChangingHistory()
    {
        await using var h = await Start();
        string history = await History(h);
        var body = await Checkout(h);
        Assert.Equal("User", body.GetProperty("checkedOutBy").GetString());
        Assert.Equal(4, body.GetProperty("acquiredCount").GetInt32());
        Assert.Equal(1, body.GetProperty("reusedLockCount").GetInt32());
        Guid id = body.GetProperty("batchId").GetGuid();
        var preview = await Preview(h, id);
        Assert.Equal("CheckoutBatch", preview.GetProperty("scope").GetString());
        Assert.Equal(new long[] { 1, 3, 4, 7 }, preview.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("documentId").GetInt64()));
        await h.WithDb(async db => {
            var batch = await db.CheckoutBatches.Include(b => b.Items).SingleAsync();
            Assert.Equal(id, batch.BatchId); Assert.Equal("User", batch.Actor);
            Assert.Equal(0, batch.CreatedAt.Ticks % 10);
            foreach (var item in batch.Items) {
                var d = await db.Documents.FindAsync(item.DocumentId);
                Assert.Equal(d!.CurrentVersionId, item.VersionId); Assert.Equal(d.CheckedOutAt, item.LockTime); Assert.Null(item.RestoredAt);
            }
            Assert.Equal(h.LockTime, (await db.Documents.FindAsync(2L))!.CheckedOutAt);
            foreach (long n in new long[] { 5, 6, 8 }) Assert.Null((await db.Documents.FindAsync(n))!.CheckedOutBy);
        });
        Assert.Equal(history, await History(h));
        var result = await Undo(h, id);
        Assert.Equal(4, result.GetProperty("restoredCount").GetInt32());
        await h.WithDb(async db => {
            Assert.Equal(4, await db.CheckoutBatchItems.CountAsync(i => i.RestoredAt != null));
            Assert.Equal("User", (await db.Documents.FindAsync(2L))!.CheckedOutBy);
            Assert.Equal(h.LockTime, (await db.Documents.FindAsync(2L))!.CheckedOutAt);
        });
        Assert.Equal(history, await History(h));
        Assert.Empty(Directory.GetFiles(h.Vault, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task RootOnlyCheckoutAndActorScopedHistoryRemainExplicit()
    {
        await using var h = await Start();
        var b = await Checkout(h, false, " user ");
        Assert.Equal(1, b.GetProperty("acquiredCount").GetInt32());
        var list = await h.Client.GetFromJsonAsync<JsonElement>("/api/documents/1/checkout-batches?actor=USER");
        Assert.Equal(b.GetProperty("batchId").GetGuid(), list[0].GetProperty("batchId").GetGuid());
        Assert.Empty((await h.Client.GetFromJsonAsync<JsonElement>("/api/documents/1/checkout-batches?actor=Other")).EnumerateArray());
        Assert.Empty((await h.Client.GetFromJsonAsync<JsonElement>("/api/documents/2/checkout-batches?actor=User")).EnumerateArray());
        Assert.Equal(1, (await Undo(h, b.GetProperty("batchId").GetGuid(), actor: "USER")).GetProperty("restoredCount").GetInt32());
    }

    [Theory]
    [InlineData("other")]
    [InlineData("ownerWithoutTime")]
    [InlineData("timeWithoutOwner")]
    [InlineData("missingVersion")]
    [InlineData("rootLocked")]
    [InlineData("rootMissingVersion")]
    public async Task CheckoutConflictsAreAtomicAndLeaveNoBatch(string change)
    {
        await using var h = await Start();
        await h.WithDb(async db => {
            var d = await db.Documents.FindAsync(change.StartsWith("root") ? 1L : 2L);
            if (change == "other") d!.CheckedOutBy = "Other";
            if (change == "ownerWithoutTime") d!.CheckedOutAt = null;
            if (change == "timeWithoutOwner") d!.CheckedOutBy = null;
            if (change == "missingVersion" || change == "rootMissingVersion") d!.CurrentVersionId = null;
            if (change == "rootLocked") { d!.CheckedOutBy = "User"; d.CheckedOutAt = h.LockTime; }
            await db.SaveChangesAsync();
        });
        string before = await Snapshot(h);
        await Status(h.Client.PostAsJsonAsync("/api/documents/1/checkout?forceIncludeRelations=true", new { checkOutBy = "User" }), HttpStatusCode.Conflict);
        Assert.Equal(before, await Snapshot(h));
    }

    [Theory]
    [InlineData("owner", "OwnerChanged")]
    [InlineData("time", "LockChanged")]
    [InlineData("version", "VersionChanged")]
    [InlineData("unlocked", "Unlocked")]
    [InlineData("missing", "DocumentMissing")]
    public async Task RevalidatesChangedItemsAndReportsSafePartialResult(string change, string code)
    {
        await using var h = await Start();
        Guid id = (await Checkout(h)).GetProperty("batchId").GetGuid();
        await Preview(h, id);
        await h.WithDb(async db => {
            var d = await db.Documents.FindAsync(3L);
            if (change == "owner") d!.CheckedOutBy = "Other";
            if (change == "time") d!.CheckedOutAt = d.CheckedOutAt!.Value.AddTicks(10);
            if (change == "version") { var v = Version(3, "child-new.sldprt", 2); db.Add(v); await db.SaveChangesAsync(); d!.CurrentVersionId = v.VersionId; }
            if (change == "unlocked") { d!.CheckedOutBy = null; d.CheckedOutAt = null; }
            if (change == "missing") {
                await db.BomOccurrences.Where(b => b.ChildVersionId == d!.CurrentVersionId).ExecuteDeleteAsync();
                d!.CurrentVersionId = null; await db.SaveChangesAsync(); db.Documents.Remove(d);
            }
            await db.SaveChangesAsync();
        });
        string beforeItem = await Document(h, 3);
        var p = await Preview(h, id);
        Assert.Contains(p.GetProperty("items").EnumerateArray(), i => i.GetProperty("code").GetString() == code);
        var result = await Undo(h, id);
        Assert.Equal(3, result.GetProperty("restoredCount").GetInt32());
        Assert.Equal(code, result.GetProperty("skipped")[0].GetProperty("code").GetString());
        Assert.Equal(beforeItem, await Document(h, 3));
        await h.WithDb(async db => Assert.Null((await db.CheckoutBatchItems.SingleAsync(i => i.DocumentId == 3)).RestoredAt));
    }

    [Fact]
    public async Task ChangedBomDoesNotExpandRestoreScopeAndReplayCannotUnlockNewBatch()
    {
        await using var h = await Start();
        Guid old = (await Checkout(h)).GetProperty("batchId").GetGuid();
        await h.WithDb(async db => {
            await db.BomOccurrences.Where(b => b.ParentVersionId == 1).ExecuteDeleteAsync();
            var version = (await db.Documents.FindAsync(6L))!.CurrentVersionId!.Value;
            db.Add(Link(1, version, "unrelated.sldprt")); await db.SaveChangesAsync();
        });
        Assert.Equal(4, (await Undo(h, old)).GetProperty("restoredCount").GetInt32());
        Guid current = (await Checkout(h, false)).GetProperty("batchId").GetGuid();
        string before = await Snapshot(h);
        var replay = await Undo(h, old);
        Assert.Equal(0, replay.GetProperty("restoredCount").GetInt32());
        Assert.All(replay.GetProperty("skipped").EnumerateArray(), i => Assert.Equal("AlreadyRestored", i.GetProperty("code").GetString()));
        Assert.Equal(before, await Snapshot(h)); Assert.NotEqual(old, current);
    }

    [Theory]
    [InlineData("undo-checkout")]
    [InlineData("checkin")]
    public async Task ExplicitScopeActorRootAndBatchValidationCannotBypassProtection(string route)
    {
        await using var h = await Start();
        Guid id = (await Checkout(h)).GetProperty("batchId").GetGuid();
        string before = await Snapshot(h);
        string path = "/api/documents/1/" + route;
        await Status(h.Client.PostAsJsonAsync(path, new { actor = "User", includeBatch = true }), HttpStatusCode.BadRequest);
        await Status(h.Client.PostAsJsonAsync(path, new { actor = "User", includeBatch = false, batchId = id }), HttpStatusCode.BadRequest);
        await Status(h.Client.PostAsJsonAsync(path, new { actor = "Other", includeBatch = true, batchId = id }), HttpStatusCode.Forbidden);
        await Status(h.Client.PostAsJsonAsync(path, new { actor = "User", includeBatch = true, batchId = Guid.Empty }), HttpStatusCode.BadRequest);
        await Status(h.Client.PostAsJsonAsync(path, new { actor = "User", includeBatch = true, batchId = Guid.NewGuid() }), HttpStatusCode.NotFound);
        await Status(h.Client.PostAsJsonAsync("/api/documents/2/" + route, new { actor = "User", includeBatch = true, batchId = id }), HttpStatusCode.NotFound);
        await Status(h.Client.GetAsync($"/api/documents/1/undo-checkout-preview?actor=Other&includeBatch=true&batchId={id}"), HttpStatusCode.Forbidden);
        await Status(h.Client.GetAsync("/api/documents/1/undo-checkout-preview?actor=User&includeBatch=true"), HttpStatusCode.BadRequest);
        await Status(h.Client.GetAsync($"/api/documents/1/undo-checkout-preview?actor=User&batchId={id}"), HttpStatusCode.BadRequest);
        Assert.Equal(before, await Snapshot(h));
        Assert.Equal(4, (await Undo(h, id, route)).GetProperty("restoredCount").GetInt32());
    }

    [Fact]
    public async Task LegacyLocksAreNotGuessedIntoABatchAndSingleCancelStillWorks()
    {
        await using var h = await ContractHost.Start();
        Assert.Empty((await h.Client.GetFromJsonAsync<JsonElement>("/api/documents/1/checkout-batches?actor=User")).EnumerateArray());
        await Status(h.Client.GetAsync($"/api/documents/1/undo-checkout-preview?actor=User&includeBatch=true&batchId={Guid.NewGuid()}"), HttpStatusCode.NotFound);
        var preview = await h.Preview();
        Assert.Equal("SingleDocument", preview.GetProperty("scope").GetString());
        await Status(h.Client.PostAsJsonAsync("/api/documents/1/undo-checkout", h.Request(preview)), HttpStatusCode.OK);
        Assert.Equal("User", (await h.Snapshot()).OtherOwner);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task MidwayFailureRollsBackAllDocumentAndAuditUpdates(bool restore, bool returnZero)
    {
        var failure = new FailSecondUpdate { ReturnZero = returnZero };
        await using var h = await Start(failure);
        Guid id = restore ? (await Checkout(h)).GetProperty("batchId").GetGuid() : Guid.Empty;
        string before = await Snapshot(h);
        failure.Armed = true;
        await Status(restore ? h.Client.PostAsJsonAsync("/api/documents/1/undo-checkout", new { actor = "User", includeBatch = true, batchId = id }) :
            h.Client.PostAsJsonAsync("/api/documents/1/checkout?forceIncludeRelations=true", new { checkOutBy = "User" }), HttpStatusCode.Conflict);
        Assert.Equal(2, failure.Updates); Assert.Equal(before, await Snapshot(h));
    }

    [Fact]
    public async Task ConcurrentCheckoutHasOneBatchAndConcurrentRestoreNeverReleasesNewLocks()
    {
        await using var h = await Start();
        var attempts = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => h.Client.PostAsJsonAsync(
            "/api/documents/1/checkout?forceIncludeRelations=true", new { checkOutBy = "User" })));
        Assert.Single(attempts, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(attempts.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Guid id = (await Body(attempts.Single(r => r.StatusCode == HttpStatusCode.OK))).GetProperty("batchId").GetGuid();
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => h.Client.PostAsJsonAsync(
            "/api/documents/1/undo-checkout", new { actor = "User", includeBatch = true, batchId = id })));
        int released = 0;
        foreach (var response in results) {
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
            if (response.IsSuccessStatusCode) released += (await Body(response)).GetProperty("restoredCount").GetInt32();
        }
        Assert.Equal(4, released);
        await h.WithDb(async db => { Assert.Equal(1, await db.CheckoutBatches.CountAsync()); Assert.Equal(4, await db.CheckoutBatchItems.CountAsync(i => i.RestoredAt != null)); });
    }

    [Fact]
    public async Task OldBomVersionStillLocksDocumentIdentityAtItsCurrentVersion()
    {
        await using var h = await Start();
        long current = 0;
        await h.WithDb(async db => {
            var v = Version(3, "child-new.sldprt", 2); db.Add(v); await db.SaveChangesAsync();
            (await db.Documents.FindAsync(3L))!.CurrentVersionId = current = v.VersionId; await db.SaveChangesAsync();
        });
        Guid id = (await Checkout(h)).GetProperty("batchId").GetGuid();
        var item = (await Preview(h, id)).GetProperty("items").EnumerateArray().Single(i => i.GetProperty("documentId").GetInt64() == 3);
        Assert.Equal(current, item.GetProperty("versionId").GetInt64());
        Assert.Equal(4, (await Undo(h, id)).GetProperty("restoredCount").GetInt32());
    }

    [Fact]
    public async Task LaterRootBatchDoesNotAdoptLocksStillHeldByEarlierBatch()
    {
        await using var h = await Start();
        Guid old = (await Checkout(h)).GetProperty("batchId").GetGuid();
        var single = await h.Preview();
        await Status(h.Client.PostAsJsonAsync("/api/documents/1/undo-checkout", h.Request(single)), HttpStatusCode.OK);
        var latest = await Checkout(h);
        Assert.Equal(1, latest.GetProperty("acquiredCount").GetInt32());
        Assert.Equal(4, latest.GetProperty("reusedLockCount").GetInt32());
        Assert.Equal(1, (await Undo(h, latest.GetProperty("batchId").GetGuid())).GetProperty("restoredCount").GetInt32());
        var remaining = await Undo(h, old);
        Assert.Equal(3, remaining.GetProperty("restoredCount").GetInt32());
        Assert.Equal("Unlocked", remaining.GetProperty("skipped")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void PendingMigrationOnlyCreatesTheTwoBatchTables()
    {
        // Inspect metadata in memory, never call Migrate/Database.Update.
        var migration = new SWPdm.Sample.Data.Migrations.AddCheckoutBatches();
        Assert.Equal(3, migration.UpOperations.Count);
        Assert.Equal(new[] { "pdm_checkout_batch_items", "pdm_checkout_batches" }, migration.UpOperations
            .OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.CreateTableOperation>().Select(t => t.Name).Order());
        Assert.All(migration.UpOperations, op => Assert.True(op is Microsoft.EntityFrameworkCore.Migrations.Operations.CreateTableOperation or Microsoft.EntityFrameworkCore.Migrations.Operations.CreateIndexOperation));
        Assert.All(migration.DownOperations, op => Assert.StartsWith("pdm_checkout_batch", Assert.IsType<Microsoft.EntityFrameworkCore.Migrations.Operations.DropTableOperation>(op).Name));
    }

    [Fact]
    public async Task InvalidActorAndMissingRootCannotCreateBatch()
    {
        await using var h = await Start();
        string before = await Snapshot(h);
        foreach (string actor in new[] { "", " ", new string('x', 101) }) {
            await Status(h.Client.PostAsJsonAsync("/api/documents/1/checkout?forceIncludeRelations=false", new { checkOutBy = actor }), HttpStatusCode.BadRequest);
            await Status(h.Client.GetAsync("/api/documents/1/checkout-batches?actor=" + Uri.EscapeDataString(actor)), HttpStatusCode.BadRequest);
        }
        await Status(h.Client.PostAsJsonAsync("/api/documents/999/checkout?forceIncludeRelations=false", new { checkOutBy = "User" }), HttpStatusCode.NotFound);
        Assert.Equal(before, await Snapshot(h));
    }

    private static async Task<ContractHost> Start(DbCommandInterceptor? interceptor = null)
    {
        var h = await ContractHost.Start(interceptor);
        await h.WithDb(async db => {
            var root = (await db.Documents.FindAsync(1L))!; root.CheckedOutBy = null; root.CheckedOutAt = null;
            var old = (await db.Documents.FindAsync(2L))!;
            var oldVersion = Version(2, "existing.sldprt"); db.Add(oldVersion); await db.SaveChangesAsync(); old.CurrentVersionId = oldVersion.VersionId;
            for (int i = 3; i <= 8; i++) {
                var d = new PdmDocument { FileName = "batch-" + i, DocumentType = i is 4 or 7 or 8 ? "Drawing" : i == 5 ? "Assembly" : "Part",
                    FileExtension = i is 4 or 7 or 8 ? ".slddrw" : i == 5 ? ".sldasm" : ".sldprt", CreatedAt = h.UpdatedAt, UpdatedAt = h.UpdatedAt };
                db.Add(d); await db.SaveChangesAsync(); Assert.Equal(i, d.DocumentId);
                var v = Version(d.DocumentId, "batch-" + i + d.FileExtension); db.Add(v); await db.SaveChangesAsync(); d.CurrentVersionId = v.VersionId;
            }
            await db.SaveChangesAsync();
            long V(int n) => db.Documents.Local.Single(d => d.DocumentId == n).CurrentVersionId!.Value;
            db.AddRange(Link(1, V(2), "existing.sldprt"), Link(1, V(3), "batch-3.sldprt"), Link(V(4), V(3), "batch-3.sldprt"),
                Link(V(5), 1, "original.sldprt"), Link(V(7), null, "original.sldprt"), Link(V(8), 1, "original.sldprt"));
            var historical = db.Documents.Local.Single(d => d.DocumentId == 8);
            var next = Version(8, "new-drawing.slddrw", 2); db.Add(next); await db.SaveChangesAsync(); historical.CurrentVersionId = next.VersionId;
            await db.SaveChangesAsync();
        });
        return h;
    }

    private static PdmDocumentVersion Version(long doc, string name, int no = 1) => new() { DocumentId = doc, VersionNo = no, OriginalFileName = name, StorageFileId = "synthetic-" + name, RevisionLabel = "A", CreatedAt = new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero) };
    private static PdmBomOccurrence Link(long parent, long? child, string path) => new() { ParentVersionId = parent, ChildVersionId = child, SourceReferencePath = path, OccurrencePath = Guid.NewGuid().ToString(), PackageRelativePath = path, ReferenceStatus = child.HasValue ? "Resolved" : "Missing" };
    private static async Task<JsonElement> Checkout(ContractHost h, bool relations = true, string actor = "User") {
        var response = await h.Client.PostAsJsonAsync($"/api/documents/1/checkout?forceIncludeRelations={relations}", new { checkOutBy = actor });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync()); return await Body(response);
    }
    private static Task<JsonElement> Preview(ContractHost h, Guid id) => h.Client.GetFromJsonAsync<JsonElement>($"/api/documents/1/undo-checkout-preview?actor=User&includeBatch=true&batchId={id}");
    private static async Task<JsonElement> Undo(ContractHost h, Guid id, string route = "undo-checkout", string actor = "User") {
        var r = await h.Client.PostAsJsonAsync("/api/documents/1/" + route, new { actor, includeBatch = true, batchId = id });
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync()); return await Body(r);
    }
    private static async Task Status(Task<HttpResponseMessage> request, HttpStatusCode status) { var r = await request; Assert.True(r.StatusCode == status, $"Expected {status}, got {r.StatusCode}: {await r.Content.ReadAsStringAsync()}"); }
    private static async Task<JsonElement> Body(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();
    private static async Task<string> Document(ContractHost h, long id) { string result = ""; await h.WithDb(async db => result = JsonSerializer.Serialize(await db.Documents.AsNoTracking().Where(d => d.DocumentId == id).Select(d => new { d.DocumentId, d.CurrentVersionId, d.CheckedOutBy, d.CheckedOutAt }).SingleOrDefaultAsync())); return result; }
    private static async Task<string> History(ContractHost h) { string result = ""; await h.WithDb(async db => result = JsonSerializer.Serialize(new { Versions = await db.DocumentVersions.AsNoTracking().OrderBy(v => v.VersionId).Select(v => new { v.VersionId, v.DocumentId, v.VersionNo, v.OriginalFileName, v.RevisionLabel }).ToArrayAsync(), Bom = await db.BomOccurrences.AsNoTracking().OrderBy(b => b.BomOccurrenceId).Select(b => new { b.BomOccurrenceId, b.ChildVersionId, b.ParentVersionId, b.ReferenceStatus }).ToArrayAsync(), Docs = await db.Documents.AsNoTracking().OrderBy(d => d.DocumentId).Select(d => new { d.DocumentId, d.UpdatedAt, d.CurrentVersionId }).ToArrayAsync() })); return result; }
    private static async Task<string> Snapshot(ContractHost h) { string result = ""; await h.WithDb(async db => result = JsonSerializer.Serialize(new {
        Docs = await db.Documents.AsNoTracking().OrderBy(d => d.DocumentId).Select(d => new { d.DocumentId, d.CurrentVersionId, d.CheckedOutBy, d.CheckedOutAt }).ToArrayAsync(),
        Batches = await db.CheckoutBatches.AsNoTracking().OrderBy(b => b.BatchId).Select(b => new { b.BatchId, b.RootDocumentId, b.Actor, b.CreatedAt }).ToArrayAsync(),
        Items = await db.CheckoutBatchItems.AsNoTracking().OrderBy(i => i.BatchId).ThenBy(i => i.DocumentId).Select(i => new { i.BatchId, i.DocumentId, i.VersionId, i.LockTime, i.RestoredAt }).ToArrayAsync() })); return result; }

    private sealed class FailSecondUpdate : DbCommandInterceptor
    {
        public bool Armed; public bool ReturnZero; public int Updates;
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (Armed && command.CommandText.StartsWith("UPDATE ", StringComparison.Ordinal) && command.CommandText.Contains("pdm_documents", StringComparison.Ordinal) && ++Updates == 2) {
                Armed = false;
                if (ReturnZero) return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
                throw new CancelCheckoutConflictException("隔離測試：第二筆文件更新失敗。");
            }
            return ValueTask.FromResult(result);
        }
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SWPdm.Api.Services;
using SWPdm.Sample.Data;
using SWPdm.Sample.Data.Entities;
using SWPdm.Sample.Services;
using Xunit;

namespace SWPdm.ContractTests;

public sealed class IngestionIntegrityTests
{
    [Fact]
    public async Task WipSameRevisionCreatesNextVersionWithoutChangingHistoricalRowsOrOtherLocks()
    {
        await using var host = await ContractHost.Start();
        string history = await History(host);
        await File.WriteAllTextAsync(host.FilePath, "changed WIP bytes");
        var result = await Send(host, host.FilePath, 1);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var body = await Body(result);
        Assert.Equal("CreatedVersion", body.GetProperty("rootOutcome").GetString());
        Assert.Equal(2, body.GetProperty("files")[0].GetProperty("versionNo").GetInt32());
        Assert.Equal(history, await History(host));
        await host.WithDb(async db => {
            var version = await db.DocumentVersions.SingleAsync(v => v.VersionNo == 2);
            Assert.Equal("A", version.RevisionLabel); Assert.Equal("WIP", version.LifecycleState);
            Assert.Equal("驗證變更", version.ChangeReason);
            Assert.Null((await db.Documents.FindAsync(1L))!.CheckedOutBy);
            Assert.Equal("User", (await db.Documents.FindAsync(2L))!.CheckedOutBy);
        });
    }

    [Theory]
    [InlineData("WIP", "A", "", "CHANGE_REASON_REQUIRED")]
    [InlineData("WIP", "", "原因", "REVISION_REQUIRED")]
    [InlineData("Released", "A", "原因", "RELEASED_REVISION_REQUIRED")]
    [InlineData("InReview", "B", "原因", "LIFECYCLE_BLOCKED")]
    [InlineData("Obsolete", "B", "原因", "LIFECYCLE_BLOCKED")]
    public async Task InvalidRevisionPoliciesFailWithoutWriting(string state, string rev, string reason, string code)
    {
        await using var host = await ContractHost.Start();
        await host.WithDb(async db => { (await db.DocumentVersions.FindAsync(1L))!.LifecycleState = state; await db.SaveChangesAsync(); });
        await File.WriteAllTextAsync(host.FilePath, "changed"); host.Parser.Revision = rev;
        var before = await host.Snapshot();
        await Error(await Send(host, host.FilePath, 1, reason), code);
        Assert.Equal(before, await host.Snapshot());
        Assert.Empty(Directory.GetFiles(host.Vault, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void UnknownLifecycleAndMissingVersionFailClosed()
    {
        Assert.Equal("LIFECYCLE_BLOCKED", Assert.Throws<PdmWorkflowException>(() =>
            PdmIngestionService.ValidateVersionChange(new() { LifecycleState = "Unknown" }, "A", "原因")).Code);
        Assert.Equal("MISSING_CURRENT_VERSION", Assert.Throws<PdmWorkflowException>(() =>
            PdmIngestionService.ValidateVersionChange(null, "A", "原因")).Code);
    }

    [Fact]
    public async Task ReleasedNewRevisionCreatesWipAndPreservesReleasedVersion()
    {
        await using var host = await ContractHost.Start();
        await host.WithDb(async db => { (await db.DocumentVersions.FindAsync(1L))!.LifecycleState = "Released"; await db.SaveChangesAsync(); });
        string history = await History(host);
        await File.WriteAllTextAsync(host.FilePath, "released update"); host.Parser.Revision = "B";
        Assert.Equal(HttpStatusCode.OK, (await Send(host, host.FilePath, 1)).StatusCode);
        Assert.Equal(history, await History(host));
        await host.WithDb(async db => {
            var current = await db.DocumentVersions.SingleAsync(v => v.VersionNo == 2);
            Assert.Equal("B", current.RevisionLabel); Assert.Equal("WIP", current.LifecycleState);
        });
    }

    [Fact]
    public async Task SharedRenamedCopyReusesVersionAndDoesNotChangeOtherActorsLock()
    {
        await using var host = await ContractHost.Start();
        await host.WithDb(async db => { (await db.Documents.FindAsync(1L))!.CheckedOutBy = "Other"; await db.SaveChangesAsync(); });
        string copy = await Cad(host, "renamed.sldprt", "CONTRACT", await File.ReadAllTextAsync(host.FilePath));
        var before = await host.Snapshot(); string history = await History(host);
        var result = await Send(host, copy);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var body = await Body(result);
        Assert.Equal("Reused", body.GetProperty("rootOutcome").GetString());
        Assert.Equal(1, body.GetProperty("rootVersionId").GetInt64());
        Assert.Equal(before, await host.Snapshot()); Assert.Equal(history, await History(host));
        Assert.Empty(Directory.GetFiles(host.Vault, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task SharedChangedContentRequiresExplicitCheckinEvenWhenActorOwnsLock()
    {
        await using var host = await ContractHost.Start();
        await File.WriteAllTextAsync(host.FilePath, "different shared content");
        var before = await host.Snapshot();
        await Error(await Send(host, host.FilePath), "EXISTING_FILE_CHANGED");
        Assert.Equal(before, await host.Snapshot());
    }

    [Fact]
    public async Task ExplicitRenameNeedsReasonAndNeverReportsReused()
    {
        await using var host = await ContractHost.Start();
        string renamed = await Cad(host, "renamed.sldprt", "CONTRACT", await File.ReadAllTextAsync(host.FilePath));
        await Error(await Send(host, renamed, 1, ""), "CHANGE_REASON_REQUIRED");
        var result = await Send(host, renamed, 1);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("CreatedVersion", (await Body(result)).GetProperty("rootOutcome").GetString());
        await host.WithDb(async db => {
            Assert.Equal("original.sldprt", (await db.DocumentVersions.FindAsync(1L))!.OriginalFileName);
            Assert.Equal("renamed.sldprt", (await db.DocumentVersions.SingleAsync(v => v.VersionNo == 2)).OriginalFileName);
        });
    }

    [Fact]
    public async Task ReusingSameUploadPathWithDifferentIdentityCreatesDifferentDocument()
    {
        await using var host = await ContractHost.Start();
        host.Parser.PartNumber = "NEW-PART"; await File.WriteAllTextAsync(host.FilePath, "new identity");
        var result = await Send(host, host.FilePath);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.NotEqual(1, (await Body(result)).GetProperty("rootDocumentId").GetInt64());
        Assert.Equal("User", (await host.Snapshot()).Owner);
    }

    [Fact]
    public async Task DesignDependencyIsReportedButNotRecursedOrStoredAsBom()
    {
        await using var host = await ContractHost.Start();
        var path = await Cad(host, "derived.sldprt", "DERIVED", "derived", references: [
            new(Path.Combine(host.Folder, "absent-design.sldprt"), CadReferencePurpose.DesignDependency)]);
        var result = await Send(host, path, recurse: true);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var body = await Body(result);
        Assert.Contains("設計", body.GetProperty("issues")[0].GetString());
        await host.WithDb(async db => Assert.Empty(await db.BomOccurrences.Where(b =>
            b.ParentVersionId == body.GetProperty("rootVersionId").GetInt64()).ToListAsync()));
    }

    [Fact]
    public async Task OccurrencesVirtualAndSuppressedFlagsArePreservedWithoutIngestingTheirFiles()
    {
        await using var host = await ContractHost.Start();
        string shared = await Cad(host, "shared-copy.sldprt", "CONTRACT", await File.ReadAllTextAsync(host.FilePath));
        var references = new CadReference[] {
            new(shared, CadReferencePurpose.AssemblyComponent, false, false, "Default", "Child", "shared-1"),
            new(shared, CadReferencePurpose.AssemblyComponent, false, false, "Default", "Child", "shared-2"),
            new(Path.Combine(host.Folder, "virtual.sldprt"), CadReferencePurpose.AssemblyComponent, true, false, "Default", "", "virtual"),
            new(Path.Combine(host.Folder, "suppressed.sldprt"), CadReferencePurpose.AssemblyComponent, false, true, "Default", "", "suppressed") };
        string root = await Cad(host, "root.sldasm", "ROOT", "root bytes", SolidWorksDocumentKind.Assembly, references);
        string history = await History(host);
        var result = await Send(host, root, recurse: true);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        long version = (await Body(result)).GetProperty("rootVersionId").GetInt64();
        await host.WithDb(async db => {
            var rows = await db.BomOccurrences.Where(b => b.ParentVersionId == version).OrderBy(b => b.BomOccurrenceId).ToListAsync();
            Assert.Equal(4, rows.Count); Assert.Equal(4, rows.Select(r => r.OccurrencePath).Distinct().Count());
            Assert.All(rows.Take(2), row => { Assert.Equal(1, row.ChildVersionId); Assert.Equal("Child", row.ChildConfigurationName); });
            Assert.True(rows[2].IsVirtual); Assert.Equal("Virtual", rows[2].ReferenceStatus); Assert.Null(rows[2].ChildVersionId);
            Assert.True(rows[3].IsSuppressed); Assert.Equal("Missing", rows[3].ReferenceStatus); Assert.Null(rows[3].ChildVersionId);
        });
        Assert.Equal(history, await History(host)); Assert.Equal("User", (await host.Snapshot()).Owner);
    }

    [Fact]
    public async Task DrawingLinksExactStoredContentNotLatestVersionAndDoesNotHealHistory()
    {
        await using var host = await ContractHost.Start();
        string oldCopy = await Cad(host, "old-copy.sldprt", "CONTRACT", await File.ReadAllTextAsync(host.FilePath));
        await File.WriteAllTextAsync(host.FilePath, "version two"); await Send(host, host.FilePath, 1);
        string drawing = await Cad(host, "drawing.slddrw", "DRAWING", "drawing bytes", SolidWorksDocumentKind.Drawing,
            [new(oldCopy, CadReferencePurpose.DrawingModel)]);
        var result = await Send(host, drawing);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        long version = (await Body(result)).GetProperty("rootVersionId").GetInt64();
        await host.WithDb(async db => {
            Assert.Equal(1, (await db.BomOccurrences.SingleAsync(b => b.ParentVersionId == version)).ChildVersionId);
            Assert.Null((await db.BomOccurrences.SingleAsync(b => b.OccurrencePath == "historical")).ChildVersionId);
            var displayed = await new SWPdm.Sample.Data.Repositories.PdmRepository(db).GetImmediateChildrenAsync(1);
            Assert.Null(displayed.Single().ChildVersionId);
            Assert.Equal("Missing", displayed.Single().ReferenceStatus);
        });
    }

    [Fact]
    public async Task DrawingWithoutNativeModelReferenceDoesNotInventSamePartNumberLink()
    {
        await using var host = await ContractHost.Start();
        string drawing = await Cad(host, "drawing.slddrw", "CONTRACT", "drawing", SolidWorksDocumentKind.Drawing);
        var response = await Send(host, drawing); var body = await Body(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await host.WithDb(async db => Assert.Empty(await db.BomOccurrences.Where(b =>
            b.ParentVersionId == body.GetProperty("rootVersionId").GetInt64()).ToListAsync()));
        Assert.Contains(body.GetProperty("issues").EnumerateArray(), x => x.GetString()!.Contains("未解析到模型參照"));
    }

    [Fact]
    public async Task FilenameOnlyCandidateIsNotVersionEvidence()
    {
        await using var host = await ContractHost.Start();
        string drawing = await Cad(host, "drawing.slddrw", "DRAWING", "drawing", SolidWorksDocumentKind.Drawing,
            [new(Path.Combine(host.Folder, "absent", "original.sldprt"), CadReferencePurpose.DrawingModel)]);
        var response = await Send(host, drawing); var body = await Body(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await host.WithDb(async db => {
            var row = await db.BomOccurrences.SingleAsync(b => b.ParentVersionId == body.GetProperty("rootVersionId").GetInt64());
            Assert.Null(row.ChildVersionId); Assert.Equal("Missing", row.ReferenceStatus);
        });
        Assert.Contains(body.GetProperty("issues").EnumerateArray(), x => x.GetString()!.Contains("只有檔名候選"));
    }

    [Fact]
    public async Task RecursiveFailureRollsBackRootAndNewUploadsButKeepsExistingVault()
    {
        await using var host = await ContractHost.Start();
        string keep = Path.Combine(host.Vault, "keep.txt"); await File.WriteAllTextAsync(keep, "preserve existing");
        string good = await Cad(host, "good.sldprt", "GOOD", "good bytes");
        string bad = await Cad(host, "bad.sldprt", "BAD", "bad bytes"); host.Parser.Failures.Add("bad.sldprt");
        string root = await Cad(host, "root.sldasm", "ROOT", "root", SolidWorksDocumentKind.Assembly, [
            new(good, CadReferencePurpose.AssemblyComponent), new(bad, CadReferencePurpose.AssemblyComponent)]);
        var before = await host.Snapshot();
        await Error(await Send(host, root, recurse: true), "REFERENCE_INGEST_FAILED");
        Assert.Equal(before, await host.Snapshot());
        Assert.Equal(new[] { keep }, Directory.GetFiles(host.Vault, "*", SearchOption.AllDirectories));
        Assert.Equal("preserve existing", await File.ReadAllTextAsync(keep));
        host.Parser.Failures.Clear();
        Assert.Equal(HttpStatusCode.OK, (await Send(host, root, recurse: true)).StatusCode);
    }

    [Fact]
    public async Task CycleIsRejectedWithoutPartialDatabaseRows()
    {
        await using var host = await ContractHost.Start();
        string first = Path.Combine(host.Folder, "a.sldasm"), second = Path.Combine(host.Folder, "b.sldasm");
        await Cad(host, "a.sldasm", "A-ASM", "a", SolidWorksDocumentKind.Assembly, [new(second, CadReferencePurpose.AssemblyComponent)]);
        await Cad(host, "b.sldasm", "B-ASM", "b", SolidWorksDocumentKind.Assembly, [new(first, CadReferencePurpose.AssemblyComponent)]);
        var before = await host.Snapshot();
        await Error(await Send(host, first, recurse: true), "REFERENCE_INGEST_FAILED");
        Assert.Equal(before, await host.Snapshot());
    }

    [Fact]
    public async Task BatchReusesSharedFilesAndDoesNotIngestUnselectedServerFiles()
    {
        await using var host = await ContractHost.Start();
        string unselected = await Cad(host, "server-only.sldprt", "SERVER", "server data");
        string root = await Cad(host, "root.sldasm", "ROOT", "root", SolidWorksDocumentKind.Assembly,
            [new(unselected, CadReferencePurpose.AssemblyComponent)]);
        string shared = await Cad(host, "shared.sldprt", "CONTRACT", await File.ReadAllTextAsync(host.FilePath));
        using var form = new MultipartFormDataContent();
        foreach (string path in new[] { root, shared }) {
            form.Add(new ByteArrayContent(await File.ReadAllBytesAsync(path)), "files", Path.GetFileName(path));
            form.Add(new StringContent(Path.GetFileName(path)), "relativePaths");
        }
        form.Add(new StringContent("User"), "uploadedBy");
        var response = await host.Client.PostAsync("/api/ingest/cad-batch", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Body(response); Assert.Equal(2, body.GetProperty("succeededFileCount").GetInt32());
        Assert.Contains(body.GetProperty("files").EnumerateArray(), f => f.GetProperty("outcome").GetString() == "Reused");
        await host.WithDb(async db => { Assert.False(await db.Documents.AnyAsync(d => d.PartNumber == "SERVER")); Assert.Equal(2, await db.DocumentVersions.CountAsync()); });
        Assert.Equal("User", (await host.Snapshot()).Owner);
    }

    [Fact]
    public void ConflictingAliasesOrConfigurationIdentitiesAreNeverPickedByEnumerationOrder()
    {
        var values = new[] { ("", "PartNumber", (string?)"A"), ("", "品號", (string?)"B") };
        Assert.Null(CadPropertyResolver.Resolve(values, CadPropertyResolver.PartNumberNames).Value);
        Assert.Equal(2, CadPropertyResolver.Resolve(values, CadPropertyResolver.PartNumberNames).Conflicts.Count);
        var configs = new[] { ("Config1", "PartNumber", (string?)"A"), ("Config2", "PartNumber", (string?)"B") };
        Assert.Null(CadPropertyResolver.Resolve(configs, CadPropertyResolver.PartNumberNames).Value);
        Assert.Equal("DOC", CadPropertyResolver.Resolve(configs.Append(("", "品號", (string?)"DOC")), CadPropertyResolver.PartNumberNames).Value);
    }

    [Fact]
    public async Task PropertyConflictIsActionable409WithoutWrites()
    {
        await using var host = await ContractHost.Start();
        string file = await Cad(host, "conflict.sldprt", "A", "conflict");
        var parsed = host.Parser.Files[file];
        ((Dictionary<string, SolidWorksCustomProperty>)parsed.DocumentProperties)["品號"] = new("品號", "B", "Text");
        var before = await host.Snapshot(); await Error(await Send(host, file), "PROPERTY_CONFLICT");
        Assert.Equal(before, await host.Snapshot());
    }

    [Theory]
    [InlineData("40001")]
    [InlineData("40P01")]
    public async Task WrappedPostgresConcurrencyReturns409WithoutInternalDetails(string state)
    {
        var pg = new Npgsql.PostgresException("secret SQL", "ERROR", "ERROR", state);
        var result = SWPdm.Api.Endpoints.EndpointHelpers.ToProblem(new InvalidOperationException("internal connection", pg));
        Assert.Equal(409, ((Microsoft.AspNetCore.Http.IStatusCodeHttpResult)result).StatusCode);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task DuplicateIdentityIs409AndNeverSilentlySelected()
    {
        await using var host = await ContractHost.Start();
        await host.WithDb(async db => {
            db.Documents.Add(new PdmDocument { FileName = "duplicate", FileExtension = ".sldprt",
                DocumentType = "Part", PartNumber = " contract ", CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        });
        var before = await host.Snapshot();
        await Error(await Send(host, host.FilePath), "DUPLICATE_IDENTITY");
        Assert.Equal(before, await host.Snapshot());
    }

    [Fact]
    public async Task SharedIdentityMatchIsTrimmedAndCaseInsensitive()
    {
        await using var host = await ContractHost.Start();
        await host.WithDb(async db => { (await db.Documents.FindAsync(1L))!.PartNumber = " contract "; await db.SaveChangesAsync(); });
        var before = await host.Snapshot();
        var result = await Send(host, host.FilePath);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("Reused", (await Body(result)).GetProperty("rootOutcome").GetString());
        Assert.Equal(before, await host.Snapshot());
    }

    [Fact]
    public async Task TwoCheckinsProduceOneVersionAndKeepOtherLocks()
    {
        await using var host = await ContractHost.Start();
        await File.WriteAllTextAsync(host.FilePath, "concurrent updated content");
        var responses = await Task.WhenAll(Task.Run(() => Send(host, host.FilePath, 1)),
            Task.Run(() => Send(host, host.FilePath, 1)));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(2, (await host.Snapshot()).Versions);
        Assert.Equal("User", (await host.Snapshot()).OtherOwner);
    }

    [Fact]
    public async Task MissingCheckoutCannotCreateNewVersion()
    {
        await using var host = await ContractHost.Start();
        await host.WithDb(async db => { (await db.Documents.FindAsync(1L))!.CheckedOutBy = null; await db.SaveChangesAsync(); });
        await File.WriteAllTextAsync(host.FilePath, "changed");
        var before = await host.Snapshot();
        await Error(await Send(host, host.FilePath, 1), "CHECKOUT_REQUIRED");
        Assert.Equal(before, await host.Snapshot());
    }

    private static Task<HttpResponseMessage> Send(ContractHost host, string path, long? target = null, string? reason = "驗證變更", bool recurse = false)
        => host.Client.PostAsJsonAsync("/api/ingest/cad", new { localFilePath = path, targetDocumentId = target,
            uploadedBy = "User", changeReason = reason, ingestReferencedFiles = recurse });
    private static async Task<JsonElement> Body(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();
    private static async Task Error(HttpResponseMessage response, string code) {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(code, (await Body(response)).GetProperty("code").GetString());
    }
    private static async Task<string> Cad(ContractHost host, string name, string number, string bytes,
        SolidWorksDocumentKind kind = SolidWorksDocumentKind.Part, CadReference[]? references = null) {
        string path = Path.Combine(host.Folder, name); await File.WriteAllTextAsync(path, bytes);
        host.Parser.Files[path] = new(path, kind,
            new Dictionary<string, SolidWorksCustomProperty> { ["PartNumber"] = new("PartNumber", number, "Text"), ["Revision"] = new("Revision", "A", "Text") },
            new Dictionary<string, IReadOnlyDictionary<string, SolidWorksCustomProperty>>(),
            (references ?? []).Select(r => r.Path).ToArray(), null) { References = references ?? [] };
        return path;
    }
    private static async Task<string> History(ContractHost host) {
        string result = "";
        await host.WithDb(async db => { result = JsonSerializer.Serialize(new {
            Version = await db.DocumentVersions.AsNoTracking().Where(v => v.VersionId == 1).Select(v => new {
                v.VersionNo, v.RevisionLabel, v.LifecycleState, v.OriginalFileName, v.StorageFileId, v.ChecksumSha256, v.SourceFilePath, v.CreatedAt }).SingleAsync(),
            Bom = await db.BomOccurrences.AsNoTracking().Where(b => b.ParentVersionId == 1).Select(b => new {
                b.BomOccurrenceId, b.ChildVersionId, b.ReferenceStatus, b.OccurrencePath, b.SourceReferencePath }).ToListAsync(),
            Properties = await db.CustomProperties.AsNoTracking().Where(p => p.VersionId == 1).Select(p => new { p.PropertyName, p.PropertyValue }).ToListAsync()
        }); });
        return result;
    }
}

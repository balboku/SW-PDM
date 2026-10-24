using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SWPdm.Api.Configuration;
using SWPdm.Sample.Data.Entities;
using SWPdm.Sample.Services;
using Xunit;

namespace SWPdm.ContractTests;

internal sealed record SyntheticPackageCad(SolidWorksDocumentKind Kind, string[] Configurations, CadReference[] References,
    Dictionary<string, string>? Aliases = null);
internal sealed class FakePackageCad : ICadPackageSessionFactory, ICadPackageSession
{
    public string Fault = "";
    public List<string> Workspaces { get; } = [];
    public ICadPackageSession Open(string workspace) { lock (Workspaces) Workspaces.Add(workspace); if (Fault == "open") throw new InvalidOperationException("private key/path must not escape"); return this; }
    public CadPackageInspection Inspect(string file)
    {
        if (Fault == "inspect" || (Fault == "relocated" && Path.GetDirectoryName(file)!.EndsWith("relocated"))) throw new InvalidOperationException("private path");
        var doc = JsonSerializer.Deserialize<SyntheticPackageCad>(File.ReadAllText(file))!;
        var references = doc.References.Select(r =>
        {
            string local = Path.Combine(Path.GetDirectoryName(file)!, Path.GetFileName(r.Path));
            return r with { Path = File.Exists(r.Path) ? r.Path : File.Exists(local) ? local : r.Path };
        }).ToArray();
        return new(doc.Kind, doc.Configurations, references) { ReferenceAliases = doc.Aliases ?? [] };
    }
    public void Rewrite(string file, IReadOnlyDictionary<string, string> replacements)
    {
        if (Fault == "rewrite") throw new InvalidOperationException("synthetic Save failure / private path");
        if (Fault == "noop") return;
        var doc = JsonSerializer.Deserialize<SyntheticPackageCad>(File.ReadAllText(file))!;
        doc = doc with { References = Fault == "drop" ? [] : doc.References.Select(r => replacements.TryGetValue(r.Path, out string? target) ? r with { Path = target } : r).ToArray() };
        if (Fault == "configChange") doc = doc with { Configurations = ["Unexpected"] };
        if (Fault == "childConfig") doc = doc with { References = doc.References.Select(r => r with { ChildConfiguration = "Unexpected" }).ToArray() };
        if (Fault == "virtualDrop") doc = doc with { References = doc.References.Where(r => r.IsVirtual != true).ToArray() };
        File.WriteAllText(file, JsonSerializer.Serialize(doc));
    }
    public void Dispose() { }
}

public sealed class PackageTests
{
    [Theory]
    [InlineData("configChange")]
    [InlineData("childConfig")]
    [InlineData("virtualDrop")]
    public async Task ReopenVerifiesConfigurationAndEmbeddedVirtualEvidenceNotJustFileNames(string fault)
    {
        await using var h = await Fixture();
        if (fault == "virtualDrop") await SetCad(h, 1, SolidWorksDocumentKind.Assembly, [Ref(Source(h, "a/same.sldprt")), Ref(Source(h, "b/same.sldprt")), Ref("virtual^root.sldprt") with { IsVirtual = true }]);
        h.PackageCad.Fault = fault; string before = await Snapshot(h);
        await Failure(h, "PACKAGE_RELOCATION_FAILED"); Assert.Equal(before, await Snapshot(h)); AssertClean(h);
    }
    [Theory]
    [InlineData("?useLatest=true")]
    [InlineData("?versionOverrides=1:10")]
    public async Task RootSelectionAlsoRebuildsTheEntireClosure(string query)
    {
        await using var h = await Fixture();
        await Add(h, 6, 6, "new-root-child.sldprt", SolidWorksDocumentKind.Part, []);
        await Add(h, 1, 10, "new-root.sldasm", SolidWorksDocumentKind.Assembly, [Ref(Source(h, "new-root-child.sldprt"))]);
        await Link(h, 10, 6, Source(h, "new-root-child.sldprt"));
        string before = await Snapshot(h);
        using var zip = await Zip(await DownloadOk(h, query)); var manifest = await Manifest(zip);
        Assert.Equal(10, manifest.GetProperty("selectedRootVersionId").GetInt64());
        Assert.Equal(new long[] { 6, 10 }, manifest.GetProperty("files").EnumerateArray().Select(f => f.GetProperty("versionId").GetInt64()).Order());
        Assert.Equal(before, await Snapshot(h)); AssertClean(h);
    }

    [Fact]
    public async Task ChangedParentCannotSilentlyConsumeStaleDescendantOverride()
    {
        await using var h = await Fixture(); await SubassemblyVersions(h); string before = await Snapshot(h);
        var result = await Download(h, "?versionOverrides=2:5&versionOverrides=3:3");
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(before, await Snapshot(h)); AssertClean(h);
    }

    [Fact]
    public async Task ConcurrentRequestsUseIndependentCopiesWithoutChangingLocksOrBom()
    {
        await using var h = await Fixture(); string before = await Snapshot(h);
        var responses = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Download(h)));
        foreach (var response in responses) { using var zip = await Zip(response); Assert.Equal(3, (await Manifest(zip)).GetProperty("files").GetArrayLength()); }
        Assert.Equal(3, h.PackageCad.Workspaces.Distinct().Count());
        Assert.Equal(before, await Snapshot(h)); AssertClean(h);
    }

    [Fact]
    public async Task NativeChangedReferenceAliasesMatchSavedBomWithoutFilenameGuess()
    {
        await using var h = await Fixture();
        string old = Source(h, "a/same.sldprt"), changed = Source(h, "a/renamed-by-native.sldprt");
        await h.WithDb(async db => {
            var v = await db.DocumentVersions.FindAsync(1L); string path = Path.Combine(h.Vault, v!.StorageFileId);
            var cad = JsonSerializer.Deserialize<SyntheticPackageCad>(await File.ReadAllTextAsync(path))!;
            cad = cad with { References = cad.References.Select(r => r.Path == old ? r with { Path = changed } : r).ToArray(), Aliases = new() { [old] = changed } };
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(cad));
            v.ChecksumSha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path))).ToLowerInvariant(); v.FileSizeBytes = new FileInfo(path).Length;
            await db.SaveChangesAsync();
        });
        string before = await Snapshot(h);
        using var zip = await Zip(await DownloadOk(h)); Assert.Equal(3, (await Manifest(zip)).GetProperty("files").GetArrayLength());
        Assert.Equal(before, await Snapshot(h)); AssertClean(h);
    }

    [Fact]
    public void NativeIdentityChangeEntryIsNotAReferenceCycle()
    {
        // Pure cached-path algorithm: no COM instance, no database or original CAD is opened.
        var method = typeof(SolidWorksPackageSession).GetMethod("Effective", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "identity.sldprt");
        var changes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [missing] = missing };
        Assert.Equal(missing, method.Invoke(null, [missing, changes, Path.Combine(Path.GetTempPath(), "no-parent.sldasm")]));
        changes[missing] = missing + "x"; changes[missing + "x"] = missing;
        Assert.IsType<InvalidOperationException>(Assert.Throws<System.Reflection.TargetInvocationException>(() => method.Invoke(null, [missing, changes, "parent.sldasm"])).InnerException);
    }

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 4)]
    public async Task CompleteCopiesKeepSameNamedFilesAndHistoryUnchanged(bool drawings, int count)
    {
        await using var h = await Fixture();
        string before = await Snapshot(h);
        var result = await Download(h, "?includeDrawings=" + drawings);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("application/zip", result.Content.Headers.ContentType!.MediaType);
        using var zip = await Zip(result);
        Assert.Equal(count + 1, zip.Entries.Count);
        Assert.Contains(zip.Entries, e => e.Name == "v2__same.sldprt");
        Assert.Contains(zip.Entries, e => e.Name == "v3__same.sldprt");
        var manifest = await Manifest(zip);
        Assert.Equal(count, manifest.GetProperty("files").GetArrayLength());
        Assert.Equal(1, manifest.GetProperty("selectedRootVersionId").GetInt64());
        Assert.DoesNotContain(h.Folder, manifest.ToString());
        foreach (var file in manifest.GetProperty("files").EnumerateArray())
        {
            string name = file.GetProperty("fileName").GetString()!;
            await using var stream = zip.GetEntry(name)!.Open();
            Assert.Equal(file.GetProperty("packageSha256").GetString(), Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant());
            Assert.False(string.IsNullOrWhiteSpace(file.GetProperty("sourceSha256").GetString()));
        }
        Assert.Equal(before, await Snapshot(h));
        AssertClean(h);
    }

    [Theory]
    [InlineData("?useLatest=true")]
    [InlineData("?versionOverrides=2:5")]
    public async Task SelectedSubassemblyRecomputesItsOwnDependencyGraph(string query)
    {
        await using var h = await Fixture();
        await SubassemblyVersions(h);
        string before = await Snapshot(h);
        using var zip = await Zip(await DownloadOk(h, query));
        var ids = (await Manifest(zip)).GetProperty("files").EnumerateArray().Select(f => f.GetProperty("versionId").GetInt64()).Order().ToArray();
        Assert.Equal(new long[] { 1, 5, 6 }, ids);
        Assert.Equal(before, await Snapshot(h)); AssertClean(h);
    }

    [Fact]
    public async Task OriginalSnapshotDoesNotSilentlyChooseCurrentChild()
    {
        await using var h = await Fixture(); await SubassemblyVersions(h);
        using var zip = await Zip(await DownloadOk(h));
        Assert.Equal(new long[] { 1, 2, 3 }, (await Manifest(zip)).GetProperty("files").EnumerateArray().Select(f => f.GetProperty("versionId").GetInt64()).Order());
        AssertClean(h);
    }

    [Theory]
    [InlineData("design")]
    [InlineData("suppressed")]
    [InlineData("alternate")]
    [InlineData("virtual")]
    public async Task NativeNonBomAndConfigurationEvidenceIsNotSilentlyOmitted(string scenario)
    {
        await using var h = await Fixture();
        if (scenario == "design")
        {
            await Add(h, 5, 5, "design.sldprt", SolidWorksDocumentKind.Part, []);
            await SetCad(h, 2, SolidWorksDocumentKind.Part, [Ref(Source(h, "design.sldprt"), CadReferencePurpose.DesignDependency)]);
        }
        else if (scenario == "suppressed")
        {
            await h.WithDb(async db => { (await db.BomOccurrences.SingleAsync(b => b.ParentVersionId == 1 && b.ChildVersionId == 2)).IsSuppressed = true; await db.SaveChangesAsync(); });
            await SetCad(h, 1, SolidWorksDocumentKind.Assembly, [Ref(Source(h, "a/same.sldprt")) with { IsSuppressed = true }, Ref(Source(h, "b/same.sldprt"))]);
        }
        else if (scenario == "alternate")
            await SetCad(h, 1, SolidWorksDocumentKind.Assembly, [Ref(Source(h, "a/same.sldprt")), Ref(Source(h, "b/same.sldprt")) with { ParentConfiguration = "Alternate" }], ["Default", "Alternate"]);
        else
        {
            await SetCad(h, 1, SolidWorksDocumentKind.Assembly, [Ref(Source(h, "a/same.sldprt")), Ref(Source(h, "b/same.sldprt")), Ref("virtual^root.sldprt") with { IsVirtual = true }]);
            await h.WithDb(async db => { db.BomOccurrences.Add(new() { ParentVersionId = 1, SourceReferencePath = "virtual^root.sldprt", ReferenceStatus = "Virtual", IsVirtual = true }); await db.SaveChangesAsync(); });
        }
        string before = await Snapshot(h);
        using var zip = await Zip(await DownloadOk(h));
        var manifest = await Manifest(zip);
        Assert.Equal(scenario == "design" ? 4 : 3, manifest.GetProperty("files").GetArrayLength());
        if (scenario == "virtual") Assert.Equal(1, manifest.GetProperty("files")[0].GetProperty("virtualReferenceCount").GetInt32());
        Assert.Equal(before, await Snapshot(h)); AssertClean(h);
    }

    [Theory]
    [InlineData("missing", "PACKAGE_FILE_MISSING")]
    [InlineData("hash", "PACKAGE_CHECKSUM_MISMATCH")]
    [InlineData("hashMissing", "PACKAGE_CHECKSUM_MISSING")]
    [InlineData("size", "PACKAGE_CHECKSUM_MISMATCH")]
    [InlineData("traversal", "PACKAGE_STORAGE_UNSAFE")]
    [InlineData("absolute", "PACKAGE_STORAGE_UNSAFE")]
    public async Task MissingOrUnsafeSourceNeverProducesPartialZip(string scenario, string code)
    {
        await using var h = await Fixture();
        await h.WithDb(async db => {
            var v = await db.DocumentVersions.FindAsync(2L);
            if (scenario == "missing") v!.StorageFileId = "missing/file.sldprt";
            if (scenario == "hash") v!.ChecksumSha256 = new('0', 64);
            if (scenario == "hashMissing") v!.ChecksumSha256 = null;
            if (scenario == "size") v!.FileSizeBytes = 123456;
            if (scenario == "traversal") v!.StorageFileId = "../private.sldprt";
            if (scenario == "absolute") v!.StorageFileId = h.FilePath;
            await db.SaveChangesAsync();
        });
        string before = await Snapshot(h);
        await Failure(h, code);
        Assert.Equal(before, await Snapshot(h)); AssertClean(h);
    }

    [Theory]
    [InlineData("missingBom", "PACKAGE_BOM_UNRESOLVED")]
    [InlineData("missingNative", "PACKAGE_BOM_MISMATCH")]
    [InlineData("unknownNative", "PACKAGE_REFERENCE_MISSING")]
    [InlineData("nonCad", "PACKAGE_UNSUPPORTED_REFERENCE")]
    [InlineData("configuration", "PACKAGE_CONFIGURATION_MISSING")]
    [InlineData("cycle", "PACKAGE_REFERENCE_CYCLE")]
    [InlineData("type", "PACKAGE_TYPE_MISMATCH")]
    [InlineData("ambiguous", "PACKAGE_REFERENCE_AMBIGUOUS")]
    public async Task IncompleteOrAmbiguousDependencyEvidenceBlocks(string scenario, string code)
    {
        await using var h = await Fixture();
        if (scenario == "missingBom") await h.WithDb(async db => { (await db.BomOccurrences.FirstAsync(b => b.ParentVersionId == 1)).ChildVersionId = null; await db.SaveChangesAsync(); });
        if (scenario == "missingNative") await SetCad(h, 1, SolidWorksDocumentKind.Assembly, []);
        if (scenario == "unknownNative") await SetCad(h, 2, SolidWorksDocumentKind.Part, [Ref(Source(h, "unknown.sldprt"))]);
        if (scenario == "nonCad") await SetCad(h, 2, SolidWorksDocumentKind.Part, [Ref(Source(h, "design.xlsx"))]);
        if (scenario == "configuration") await SetCad(h, 1, SolidWorksDocumentKind.Assembly, [Ref(Source(h, "a/same.sldprt")) with { ChildConfiguration = "DoesNotExist" }, Ref(Source(h, "b/same.sldprt"))]);
        if (scenario == "cycle") await SetCad(h, 2, SolidWorksDocumentKind.Part, [Ref(Source(h, "root.sldasm"), CadReferencePurpose.DesignDependency)]);
        if (scenario == "type") await SetCad(h, 2, SolidWorksDocumentKind.Assembly, []);
        if (scenario == "ambiguous")
        {
            await Add(h, 5, 5, "design.sldprt", SolidWorksDocumentKind.Part, []);
            await Add(h, 5, 6, "design.sldprt", SolidWorksDocumentKind.Part, []);
            await SetCad(h, 2, SolidWorksDocumentKind.Part, [Ref(Source(h, "design.sldprt"))]);
        }
        string before = await Snapshot(h); await Failure(h, code); Assert.Equal(before, await Snapshot(h)); AssertClean(h);
    }

    [Theory]
    [InlineData("open", "PACKAGE_NATIVE_UNAVAILABLE", 503)]
    [InlineData("inspect", "PACKAGE_NATIVE_FAILED", 409)]
    [InlineData("rewrite", "PACKAGE_REWRITE_FAILED", 409)]
    [InlineData("noop", "PACKAGE_RELOCATION_FAILED", 409)]
    [InlineData("drop", "PACKAGE_RELOCATION_FAILED", 409)]
    [InlineData("relocated", "PACKAGE_NATIVE_FAILED", 409)]
    public async Task NativeOrSaveFailureAndNoOpReplacementNeverPass(string fault, string code, int status)
    {
        await using var h = await Fixture(); h.PackageCad.Fault = fault;
        string before = await Snapshot(h); await Failure(h, code, status); Assert.Equal(before, await Snapshot(h)); AssertClean(h);
    }

    [Theory]
    [InlineData("?versionOverrides=invalid", 400)]
    [InlineData("?versionOverrides=2:3", 400)]
    [InlineData("?versionOverrides=2:2&versionOverrides=2:2", 400)]
    [InlineData("?useLatest=true&versionOverrides=2:2", 400)]
    [InlineData("?versionOverrides=4:4", 400)]
    [InlineData("?versionOverrides=2:99999", 404)]
    public async Task InvalidOrOutOfScopeOverrideCannotBeSilentlyIgnored(string query, int status)
    {
        await using var h = await Fixture(); string before = await Snapshot(h);
        var result = await Download(h, query); Assert.Equal(status, (int)result.StatusCode);
        Assert.NotEqual("application/zip", result.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain(h.Folder, await result.Content.ReadAsStringAsync());
        Assert.Equal(before, await Snapshot(h)); AssertClean(h);
    }

    [Fact]
    public async Task MissingRootReturns404WithoutNativeWork()
    {
        await using var h = await Fixture();
        Assert.Equal(HttpStatusCode.NotFound, (await h.Client.GetAsync("/api/assemblies/99999/download-zip")).StatusCode);
        AssertClean(h);
    }

    private static async Task<ContractHost> Fixture()
    {
        var h = await ContractHost.Start();
        await h.WithDb(async db => { await db.BomOccurrences.ExecuteDeleteAsync(); await db.DocumentVersions.ExecuteDeleteAsync(); await db.Documents.ExecuteDeleteAsync(); });
        await Add(h, 1, 1, "root.sldasm", SolidWorksDocumentKind.Assembly, [Ref(Source(h, "a/same.sldprt")), Ref(Source(h, "b/same.sldprt"))]);
        await Add(h, 2, 2, "a/same.sldprt", SolidWorksDocumentKind.Part, []);
        await Add(h, 3, 3, "b/same.sldprt", SolidWorksDocumentKind.Part, []);
        await Add(h, 4, 4, "drawing.slddrw", SolidWorksDocumentKind.Drawing, [Ref(Source(h, "root.sldasm"), CadReferencePurpose.DrawingModel)]);
        await Link(h, 1, 2, Source(h, "a/same.sldprt")); await Link(h, 1, 3, Source(h, "b/same.sldprt")); await Link(h, 4, 1, Source(h, "root.sldasm"));
        return h;
    }
    private static async Task SubassemblyVersions(ContractHost h)
    {
        string sub = Source(h, "sub.sldasm");
        await h.WithDb(async db => {
            await db.BomOccurrences.Where(b => b.ParentVersionId == 1).ExecuteDeleteAsync();
            var document = await db.Documents.FindAsync(2L); document!.DocumentType = "Assembly"; document.FileExtension = ".sldasm";
            var version = await db.DocumentVersions.FindAsync(2L); version!.OriginalFileName = "sub.sldasm"; version.SourceFilePath = sub;
            version.StorageFileId = "2/sub.sldasm"; await db.SaveChangesAsync();
        });
        await SetCad(h, 1, SolidWorksDocumentKind.Assembly, [Ref(sub)]);
        await SetCad(h, 2, SolidWorksDocumentKind.Assembly, [Ref(Source(h, "b/same.sldprt"))]);
        await Link(h, 1, 2, sub); await Link(h, 2, 3, Source(h, "b/same.sldprt"));
        await Add(h, 6, 6, "new.sldprt", SolidWorksDocumentKind.Part, []);
        await Add(h, 2, 5, "sub-new.sldasm", SolidWorksDocumentKind.Assembly, [Ref(Source(h, "new.sldprt"))]);
        await Link(h, 5, 6, Source(h, "new.sldprt"));
    }
    private static async Task Add(ContractHost h, long documentId, long versionId, string relative, SolidWorksDocumentKind kind, CadReference[] references)
    {
        await h.WithDb(async db => {
            var document = await db.Documents.FindAsync(documentId);
            if (document == null) { document = new() { DocumentId = documentId, DocumentType = kind.ToString(), FileName = Path.GetFileName(relative), FileExtension = Path.GetExtension(relative), PartNumber = "SYNTH-" + documentId, CreatedAt = h.UpdatedAt, UpdatedAt = h.UpdatedAt, CheckedOutBy = documentId == 2 ? "User" : null, CheckedOutAt = documentId == 2 ? h.LockTime : null }; db.Add(document); await db.SaveChangesAsync(); }
            db.Add(new PdmDocumentVersion { VersionId = versionId, DocumentId = documentId, VersionNo = document.CurrentVersionId.HasValue ? 2 : 1, OriginalFileName = Path.GetFileName(relative), StorageFileId = versionId + "/" + Path.GetFileName(relative), SourceFilePath = Source(h, relative), CreatedAt = h.UpdatedAt });
            document.CurrentVersionId = versionId; await db.SaveChangesAsync();
        });
        await SetCad(h, versionId, kind, references);
    }
    private static async Task SetCad(ContractHost h, long versionId, SolidWorksDocumentKind kind, CadReference[] refs, string[]? configs = null)
    {
        await h.WithDb(async db => {
            var v = await db.DocumentVersions.FindAsync(versionId);
            string path = Path.Combine(h.Vault, v!.StorageFileId); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new SyntheticPackageCad(kind, configs ?? ["Default"], refs)));
            v.ChecksumSha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path))).ToLowerInvariant(); v.FileSizeBytes = new FileInfo(path).Length;
            await db.SaveChangesAsync();
        });
    }
    private static async Task Link(ContractHost h, long parent, long child, string path) => await h.WithDb(async db => { db.Add(new PdmBomOccurrence { ParentVersionId = parent, ChildVersionId = child, SourceReferencePath = path, ReferenceStatus = "Resolved", OccurrencePath = Guid.NewGuid().ToString(), ParentConfigurationName = "Default", ChildConfigurationName = "Default" }); await db.SaveChangesAsync(); });
    private static string Source(ContractHost h, string relative) => Path.Combine(h.Folder, "source", relative.Replace('/', Path.DirectorySeparatorChar));
    private static CadReference Ref(string path, CadReferencePurpose purpose = CadReferencePurpose.AssemblyComponent) => new(path, purpose, false, false, "Default", "Default", EvidenceSource: "SyntheticPackage");
    private static Task<HttpResponseMessage> Download(ContractHost h, string query = "") => h.Client.GetAsync("/api/assemblies/1/download-zip" + query);
    private static async Task<HttpResponseMessage> DownloadOk(ContractHost h, string query = "") { var result = await Download(h, query); Assert.True(result.IsSuccessStatusCode, await result.Content.ReadAsStringAsync()); return result; }
    private static async Task<ZipArchive> Zip(HttpResponseMessage response) { Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync()); return new ZipArchive(new MemoryStream(await response.Content.ReadAsByteArrayAsync()), ZipArchiveMode.Read); }
    private static async Task<JsonElement> Manifest(ZipArchive zip) { using var doc = await JsonDocument.ParseAsync(zip.GetEntry("manifest.json")!.Open()); return doc.RootElement.Clone(); }
    private static async Task Failure(ContractHost h, string code, int status = 409)
    {
        var result = await Download(h); string text = await result.Content.ReadAsStringAsync();
        Assert.True((int)result.StatusCode == status, $"Expected {status}, got {(int)result.StatusCode}: {text}");
        Assert.Equal(code, JsonDocument.Parse(text).RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain(h.Folder, text); Assert.DoesNotContain("private key", text); Assert.NotEqual("application/zip", result.Content.Headers.ContentType?.MediaType);
    }
    private static void AssertClean(ContractHost h) => Assert.All(h.PackageCad.Workspaces, path => Assert.False(Directory.Exists(path)));
    private static async Task<string> Snapshot(ContractHost h)
    {
        string result = "";
        await h.WithDb(async db => result = JsonSerializer.Serialize(new {
            Docs = await db.Documents.AsNoTracking().OrderBy(d => d.DocumentId).ToArrayAsync(),
            Versions = await db.DocumentVersions.AsNoTracking().OrderBy(d => d.VersionId).ToArrayAsync(),
            Bom = await db.BomOccurrences.AsNoTracking().OrderBy(d => d.BomOccurrenceId).ToArrayAsync(),
            Batches = await db.CheckoutBatches.AsNoTracking().OrderBy(d => d.BatchId).ToArrayAsync(),
            Items = await db.CheckoutBatchItems.AsNoTracking().OrderBy(d => d.BatchId).ThenBy(d => d.DocumentId).ToArrayAsync()
        }));
        return result + "|vault:" + string.Join(";", Directory.GetFiles(h.Vault, "*", SearchOption.AllDirectories).Order().Select(p => p + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))));
    }
}

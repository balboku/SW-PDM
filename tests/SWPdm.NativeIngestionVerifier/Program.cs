using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SWPdm.Api.Configuration;
using SWPdm.Api.Services;
using SWPdm.Sample.Data;
using SWPdm.Sample.Services;
#if SOLIDWORKS_DOCUMENT_MANAGER
using SolidWorks.Interop.swdocumentmgr;

// No live config/DB loader. Explicit key, private sample copies, memory DB, private Vault.
if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Native verifier requires Windows.");
if (args.Length != 1) throw new ArgumentException("Supply a private swpdm_dev012_native_<GUID> directory.");
string root = Path.GetFullPath(args[0]);
if (!root.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) ||
    !Path.GetFileName(root).StartsWith("swpdm_dev012_native_", StringComparison.Ordinal))
    throw new InvalidOperationException("Only this task's temporary sample directory is allowed.");
string key = Environment.GetEnvironmentVariable("SWPDM_DEV012_DM_KEY") ?? "";
if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Explicit native DM key is required.");
var files = Directory.GetFiles(Path.Combine(root, "cad"), "*", SearchOption.AllDirectories)
    .Where(p => new[] { ".sldprt", ".sldasm", ".slddrw" }.Contains(Path.GetExtension(p).ToLowerInvariant())).ToArray();
Check(files.Length > 0, "No real samples: cannot pass.");
var hashes = files.ToDictionary(p => p, Hash, StringComparer.OrdinalIgnoreCase);
var paths = files.Select(Path.GetDirectoryName).Select(p => p!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
var counts = new Dictionary<SolidWorksDocumentKind, int>();
int occurrences = 0, pathCount = 0;
using var parser = new SolidWorksDocumentManagerService(key);
var factory = new SwDMClassFactory(); var app = (SwDMApplication4)factory.GetApplication(key);
try
{
    foreach (string file in files)
    {
        var parsed = parser.Parse(file, paths);
        counts[parsed.DocumentType] = counts.GetValueOrDefault(parsed.DocumentType) + 1;
        var type = Path.GetExtension(file).ToLowerInvariant() switch {
            ".sldasm" => SwDmDocumentType.swDmDocumentAssembly,
            ".slddrw" => SwDmDocumentType.swDmDocumentDrawing, _ => SwDmDocumentType.swDmDocumentPart };
        var doc = (SwDMDocument18)app.GetDocument(file, type, true, out var error);
        Check(doc != null && error == SwDmDocumentOpenError.swDmDocumentOpenErrorNone, "Independent native open failed.");
        SwDMSearchOption? search = null; SwDMConfigurationMgr? manager = null;
        SwDMConfiguration? config = null; SwDMExternalReferenceOption2? feature = null;
        try
        {
            search = app.GetSearchOptionObject();
            search.SearchFilters = (int)(SwDmSearchFilters.SwDmSearchExternalReference | SwDmSearchFilters.SwDmSearchForPart |
                SwDmSearchFilters.SwDmSearchForAssembly | SwDmSearchFilters.SwDmSearchInContextReference);
            foreach (string path in paths) search.AddSearchPath(path);
            var saved = Strings(doc!.GetAllExternalReferences4(search, out object statuses, out object virtuals, out object timestamps));
            var expected = saved.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (type != SwDmDocumentType.swDmDocumentDrawing)
            {
                manager = doc.ConfigurationManager; string active = manager.GetActiveConfigurationName();
                feature = app.GetExternalReferenceOptionObject2();
                feature.Configuration = active; feature.NeedSuppress = true; feature.SearchOption = search;
                _ = doc.GetExternalFeatureReferences2(feature); expected.UnionWith(Strings(feature.ExternalReferences));
                if (type == SwDmDocumentType.swDmDocumentAssembly)
                {
                    config = manager.GetConfigurationByName(active);
                    var components = ((ISwDMConfiguration2)config).GetComponents() as Array;
                    var actual = parsed.ReferenceDetails.Where(r => r.Purpose == CadReferencePurpose.AssemblyComponent).ToList();
                    Check(actual.Count == (components?.Length ?? 0), "Direct component occurrence count mismatch.");
                    if (components != null) foreach (object item in components)
                    {
                        try {
                            var c = (ISwDMComponent6)item;
                            var match = actual.SingleOrDefault(r => r.OccurrenceName == c.Name && r.Path.Equals(c.PathName, StringComparison.OrdinalIgnoreCase));
                            Check(match != null && match.IsVirtual == c.IsVirtual && match.IsSuppressed == c.IsSuppressed() &&
                                match.ParentConfiguration == active && match.ChildConfiguration == c.ConfigurationName, "Native occurrence/flag/config mismatch.");
                            expected.Add(c.PathName); occurrences++;
                        } finally { Release(item); }
                    }
                }
                else Check(parsed.ReferenceDetails.All(r => !r.IsBom), "Part design references became BOM.");
            }
            else {
                Check(parsed.ReferenceDetails.All(r => r.Purpose == CadReferencePurpose.DrawingModel), "Drawing purpose mismatch.");
                for (int i = 0; i < saved.Length; i++) {
                    var actual = parsed.ReferenceDetails.Single(r => r.Path.Equals(saved[i], StringComparison.OrdinalIgnoreCase));
                    bool? flag = virtuals is Array flags && i < flags.Length ? Convert.ToBoolean(flags.GetValue(i + flags.GetLowerBound(0))) : null;
                    Check(actual.IsVirtual == flag, "Drawing native flag mismatch.");
                }
            }
            Check(expected.SetEquals(parsed.ReferencedFilePaths), "Independent native reference path union mismatch.");
            pathCount += expected.Count;
        }
        finally { doc?.CloseDoc(); Release(config); Release(manager); Release(feature); Release(search); Release(doc); }
    }
}
finally { Release(app); Release(factory); }
Check(counts.ContainsKey(SolidWorksDocumentKind.Part) && counts.ContainsKey(SolidWorksDocumentKind.Assembly) &&
    counts.ContainsKey(SolidWorksDocumentKind.Drawing), "Samples must cover Part/Assembly/Drawing.");

await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
await using var db = new PdmDbContext(new DbContextOptionsBuilder<PdmDbContext>().UseSqlite(connection).Options);
await db.Database.EnsureCreatedAsync(); // Not migration. No network DB connection.
var options = Options.Create(new SolidWorksDocumentManagerOptions { LicenseKey = key });
var ingestion = new PdmIngestionService(db, Options.Create(new LocalStorageOptions()), options,
    new LocalStorageService(Path.Combine(root, "vault", Guid.NewGuid().ToString("N"))), new SolidWorksDocumentManagerServiceFactory(options),
    NullLogger<PdmIngestionService>.Instance);
string selected = files.First(f => parser.Parse(f, paths).DocumentType == SolidWorksDocumentKind.Part &&
    CadPropertyResolver.Resolve(parser.Parse(f, paths), CadPropertyResolver.PartNumberNames).Value != null);
var first = await ingestion.IngestAsync(new(selected, null, false, paths, "NativeQC", "native parser integration"));
var original = await db.DocumentVersions.SingleAsync(v => v.VersionId == first.RootVersionId);
string? revision = original.RevisionLabel; string originalName = original.OriginalFileName; string originalHash = original.ChecksumSha256!;
string copy = Path.Combine(root, "renamed-native-" + Guid.NewGuid().ToString("N") + Path.GetExtension(selected)); File.Copy(selected, copy);
var reused = await ingestion.IngestAsync(new(copy, null, false, paths, "Other", "shared reuse"));
Check(reused.RootOutcome == "Reused" && reused.RootVersionId == first.RootVersionId && await db.DocumentVersions.CountAsync() == 1,
    "Native shared renamed content was not reused.");
var document = await db.Documents.SingleAsync(); document.CheckedOutBy = "NativeQC"; document.CheckedOutAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync();
// A protected explicit rename creates a new Ver with the same real native engineering Rev.
var changed = await ingestion.IngestAsync(new(copy, null, false, paths, "NativeQC", "explicit native rename", first.RootDocumentId));
var current = await db.DocumentVersions.SingleAsync(v => v.VersionId == changed.RootVersionId);
Check(changed.RootOutcome == "CreatedVersion" && current.VersionNo == 2 && current.RevisionLabel == revision &&
    document.CheckedOutBy == null && original.OriginalFileName == originalName && original.ChecksumSha256 == originalHash,
    "Native WIP rename/version/history invariant failed.");
document.CheckedOutBy = "NativeQC"; document.CheckedOutAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync();
parser.WriteCustomProperty(copy, "DEV012QC", "private content update");
Check(!Hash(copy).Equals(originalHash, StringComparison.OrdinalIgnoreCase), "Private native content did not change.");
var contentUpdate = await ingestion.IngestAsync(new(copy, null, false, paths, "NativeQC", "native same Rev content update", first.RootDocumentId));
var third = await db.DocumentVersions.SingleAsync(v => v.VersionId == contentUpdate.RootVersionId);
Check(third.VersionNo == 3 && third.RevisionLabel == revision && third.ChecksumSha256 != originalHash &&
    document.CheckedOutBy == null && original.ChecksumSha256 == originalHash, "Native changed-content same Rev update failed.");
int bomRoots = 0, changedSharedBlocked = 0;
foreach (string file in files.Where(f => Path.GetExtension(f).ToLowerInvariant() != ".sldprt"))
{
    var parsed = parser.Parse(file, paths);
    int beforeCount = await db.DocumentVersions.CountAsync();
    try { await ingestion.IngestAsync(new(file, null, false, paths, "NativeQC", "native shared policy")); }
    catch (PdmWorkflowException error) when (error.Code == "EXISTING_FILE_CHANGED")
    {
        // The two sample archives contain the same assembly identity with different saved references.
        Check(await db.DocumentVersions.CountAsync() == beforeCount, "Native changed shared file mutated the DB.");
        changedSharedBlocked++;
    }
    // Independent roots use independent memory databases: do not weaken the shared-content policy for a fixture.
    await using var rootConnection = new SqliteConnection("Data Source=:memory:"); await rootConnection.OpenAsync();
    await using var rootDb = new PdmDbContext(new DbContextOptionsBuilder<PdmDbContext>().UseSqlite(rootConnection).Options);
    await rootDb.Database.EnsureCreatedAsync();
    var rootIngestion = new PdmIngestionService(rootDb, Options.Create(new LocalStorageOptions()), options,
        new LocalStorageService(Path.Combine(root, "vault", Guid.NewGuid().ToString("N"))),
        new SolidWorksDocumentManagerServiceFactory(options), NullLogger<PdmIngestionService>.Instance);
    var result = await rootIngestion.IngestAsync(new(file, null, false, paths, "NativeQC", "native typed BOM integration"));
    var rows = await rootDb.BomOccurrences.Where(b => b.ParentVersionId == result.RootVersionId).OrderBy(b => b.BomOccurrenceId).ToListAsync();
    var refs = parsed.ReferenceDetails.Where(r => r.IsBom).ToArray();
    Check(rows.Count == refs.Length, "Real typed references did not reach BOM.");
    for (int i = 0; i < refs.Length; i++)
        Check(rows[i].SourceReferencePath.Equals(Path.GetFullPath(refs[i].Path), StringComparison.OrdinalIgnoreCase) &&
            rows[i].IsVirtual == (refs[i].IsVirtual == true) && rows[i].IsSuppressed == (refs[i].IsSuppressed == true) &&
            rows[i].ParentConfigurationName == refs[i].ParentConfiguration &&
            rows[i].ChildConfigurationName == refs[i].ChildConfiguration, "Native BOM path/configuration/flag mismatch.");
    bomRoots++;
}
Check(hashes.All(p => Hash(p.Key) == p.Value), "Private CAD source copies changed.");
Console.WriteLine($"NATIVE_PASS files={files.Length}; Part={counts.GetValueOrDefault(SolidWorksDocumentKind.Part)}; Assembly={counts.GetValueOrDefault(SolidWorksDocumentKind.Assembly)}; Drawing={counts.GetValueOrDefault(SolidWorksDocumentKind.Drawing)}; paths={pathCount}; occurrences={occurrences}; bomRoots={bomRoots}; changedSharedBlocked={changedSharedBlocked}; ingestion/sharedReuse/sameRevRename/sameRevContentUpdate/sourceHashes=PASS");
return 0;
static string Hash(string p) { using var s = File.OpenRead(p); return Convert.ToHexString(SHA256.HashData(s)); }
static string[] Strings(object? o) => o is Array a ? a.Cast<object>().Select(x => x.ToString()!).ToArray() : [];
static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
static void Release(object? o) { if (o != null && Marshal.IsComObject(o)) Marshal.ReleaseComObject(o); }
#else
if (args.Length == 1 && args[0] == "--verify-no-native")
{
    using var parser = new SolidWorksDocumentManagerService("isolated-no-native-test-key");
    try { parser.Parse("never-a-real-CAD.sldprt"); }
    catch (NotSupportedException) { Console.WriteLine("NO_NATIVE_FAIL_CLOSED_PASS; no fake identity or successful parse"); return 0; }
    throw new InvalidOperationException("Missing native interop must not generate a fake CAD result.");
}
Console.Error.WriteLine("Native interop unavailable; native verification NOT RUN.");
return 2;
#endif

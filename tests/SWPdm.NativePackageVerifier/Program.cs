using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SWPdm.Api.Configuration;
using SWPdm.Api.Services;
using SWPdm.Sample.Data;
using SWPdm.Sample.Data.Entities;
using SWPdm.Sample.Services;
#if SOLIDWORKS_DOCUMENT_MANAGER
using SolidWorks.Interop.swdocumentmgr;

if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
if (args.Length != 1) throw new ArgumentException("Supply a new private swpdm_dev014_native_<GUID> directory.");
string root = Path.GetFullPath(args[0]);
if (Path.GetDirectoryName(root) != Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) ||
    !Path.GetFileName(root).StartsWith("swpdm_dev014_native_", StringComparison.Ordinal) ||
    !Guid.TryParseExact(Path.GetFileName(root)["swpdm_dev014_native_".Length..], "N", out _)) throw new InvalidOperationException("Not a private test directory.");
string key = Environment.GetEnvironmentVariable("SWPDM_DEV014_DM_KEY") ?? "";
if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Explicit native key required; never loads database config.");
string[] sourceFiles = Directory.GetFiles(Path.Combine(root, "cad"), "*", SearchOption.AllDirectories).Where(Cad).ToArray();
Check(sourceFiles.Length > 0, "No native CAD evidence.");
var sourceHashes = sourceFiles.ToDictionary(p => p, Hash);
int packages = 0, verifiedFiles = 0, verifiedReferences = 0;
bool collisionVerified = false;
foreach (string group in Directory.GetDirectories(Path.Combine(root, "cad")))
{
    string workspace = Path.Combine(Path.GetTempPath(), "swpdm_package_" + Guid.NewGuid().ToString("N"));
    string input = Path.Combine(workspace, "inputs"); Directory.CreateDirectory(input);
    try
    {
        int inputIndex = 0;
        foreach (string file in Directory.GetFiles(group, "*", SearchOption.AllDirectories).Where(Cad))
        {
            string isolatedParent = Path.Combine(input, (++inputIndex).ToString()); Directory.CreateDirectory(isolatedParent);
            File.Copy(file, Path.Combine(isolatedParent, Path.GetFileName(file)));
        }
        // Separate parent folders avoid the fixture's sibling search rewriting the saved-path evidence before seeding.
        var files = Directory.GetFiles(input, "*", SearchOption.AllDirectories).Where(Cad).Order().ToArray();
        var inspections = new Dictionary<string, CadPackageInspection>(StringComparer.OrdinalIgnoreCase);
        using (var session = new SolidWorksPackageSession(key, workspace)) foreach (string file in files) inspections[file] = session.Inspect(file);
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new PdmDbContext(new DbContextOptionsBuilder<PdmDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(); // Brand-new synthetic memory DB only; no migration/network config.
        var storage = new LocalStorageService(Path.Combine(root, "vault", Guid.NewGuid().ToString("N")));
        var ids = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in files)
        {
            long id = ids.Count + 1; ids[file] = id;
            var document = new PdmDocument { DocumentId = id, DocumentType = inspections[file].Kind.ToString(), FileName = Path.GetFileName(file),
                FileExtension = Path.GetExtension(file).ToLowerInvariant(), PartNumber = "PRIVATE-" + id, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            db.Add(document); await db.SaveChangesAsync();
            db.Add(new PdmDocumentVersion { VersionId = id, DocumentId = id, VersionNo = 1, OriginalFileName = Path.GetFileName(file), SourceFilePath = file,
                StorageFileId = await storage.UploadFileAsync(file, document.DocumentType), ChecksumSha256 = Hash(file), FileSizeBytes = new FileInfo(file).Length, CreatedAt = document.CreatedAt });
            document.CurrentVersionId = id; await db.SaveChangesAsync();
        }
        foreach (var item in inspections)
        foreach (var reference in item.Value.References.Where(r => r.IsVirtual != true))
        {
            var candidates = files.Where(f => Path.GetFileName(f).Equals(Path.GetFileName(reference.Path), StringComparison.OrdinalIgnoreCase)).ToArray();
            Check(candidates.Length == 1, $"Native fixture has no unique supplied dependency: {Path.GetFileName(reference.Path)}");
            // Native evidence + supplied private files seed only a disposable fixture, never a live BOM repair.
            db.BomOccurrences.Add(new PdmBomOccurrence { ParentVersionId = ids[item.Key], ChildVersionId = ids[candidates[0]], SourceReferencePath = reference.Path,
                ParentConfigurationName = reference.ParentConfiguration, ChildConfigurationName = reference.ChildConfiguration,
                IsSuppressed = reference.IsSuppressed == true, ReferenceStatus = "Resolved", OccurrencePath = Guid.NewGuid().ToString("N"), CreatedAt = DateTimeOffset.UtcNow });
        }
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        string before = await Snapshot(db);
        var vaultHashes = files.ToDictionary(f => storage.GetFilePath(db.DocumentVersions.AsNoTracking().Single(v => v.VersionId == ids[f]).StorageFileId), Hash);
        var service = new CadPackageService(db, storage, new CadPackageSessionFactory(Options.Create(new SolidWorksDocumentManagerOptions { LicenseKey = key })));
        var cases = files.Where(f => inspections[f].Kind != SolidWorksDocumentKind.Part).Select(f => (Path: f, Collision: false)).ToList();
        if (!inspections.Values.Any(i => i.Kind == SolidWorksDocumentKind.Drawing))
            cases.Add((files.First(f => inspections[f].Kind == SolidWorksDocumentKind.Assembly), true));
        foreach (var testCase in cases)
        {
            string selected = testCase.Path;
            if (testCase.Collision)
            {
                // Deliberately give the supplied private part versions the same download name. Native bytes stay unchanged.
                await db.DocumentVersions.Where(v => v.Document.DocumentType == "Part")
                    .ExecuteUpdateAsync(s => s.SetProperty(v => v.OriginalFileName, "same.sldprt"));
                before = await Snapshot(db);
            }
            byte[] bytes;
            try { bytes = await service.BuildAsync(ids[selected], includeDrawings: inspections[selected].Kind == SolidWorksDocumentKind.Assembly); }
            catch (PackageBuildException error)
            {
                Console.WriteLine($"NATIVE_PACKAGE_FAIL code={error.Code}; files={string.Join(',', error.Issues.Select(i => i.FileName))}");
                string debug = Path.Combine(workspace, "debug"); Directory.CreateDirectory(debug);
                using var probe = new SolidWorksPackageSession(key, workspace);
                foreach (string file in files) File.Copy(file, Path.Combine(debug, "v" + ids[file] + "__" + Path.GetFileName(file)));
                foreach (string file in files.Where(f => inspections[f].References.Count > 0))
                {
                    string copy = Path.Combine(debug, "v" + ids[file] + "__" + Path.GetFileName(file));
                    var replacements = inspections[file].References.Where(r => r.IsVirtual != true).Select(r => r.Path).Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(p => p, p => Path.Combine(debug, "v" + ids[files.Single(f => Path.GetFileName(f).Equals(Path.GetFileName(p), StringComparison.OrdinalIgnoreCase))] + "__" + Path.GetFileName(p)), StringComparer.OrdinalIgnoreCase);
                    probe.Rewrite(copy, replacements);
                    var after = probe.Inspect(copy);
                    Console.WriteLine("AFTER_REWRITE " + Path.GetFileName(file) + " " + JsonSerializer.Serialize(after));
                    ProbeCache(copy, key);
                }
                throw;
            }
            string destination = Path.Combine(root, "extracted", "case-" + (++packages) + "-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(destination);
            using (var zip = new ZipArchive(new MemoryStream(bytes))) zip.ExtractToDirectory(destination);
            using var manifestDoc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(destination, "manifest.json")));
            foreach (var entry in manifestDoc.RootElement.GetProperty("files").EnumerateArray())
            {
                string path = Path.Combine(destination, entry.GetProperty("fileName").GetString()!);
                Check(Hash(path) == entry.GetProperty("packageSha256").GetString(), "Extracted hash differs from manifest.");
                Check(sourceHashes.Values.Contains(entry.GetProperty("sourceSha256").GetString()), "Source identity not in supplied CAD evidence.");
                verifiedReferences += IndependentOpen(path, destination, entry, key); verifiedFiles++;
            }
            Check(await Snapshot(db) == before, "Package changed synthetic metadata/BOM.");
            foreach (var pair in vaultHashes) Check(Hash(pair.Key) == pair.Value, "Package changed a private Vault original.");
            if (testCase.Collision)
            {
                Check(Directory.GetFiles(destination, "*__same.sldprt").Length == files.Count(f => inspections[f].Kind == SolidWorksDocumentKind.Part), "Native filename collision lost a file.");
                collisionVerified = true;
            }
        }
    }
    finally { if (Directory.Exists(workspace)) Directory.Delete(workspace, true); } // Exact owned GUID workspace.
}
foreach (var pair in sourceHashes) Check(Hash(pair.Key) == pair.Value, "Supplied CAD copy changed.");
Check(packages >= 4 && verifiedReferences > 0 && collisionVerified, "Insufficient native package cases.");
await File.WriteAllTextAsync(Path.Combine(root, "native-package-result.json"), JsonSerializer.Serialize(new {
    passed = true, sourceFileCount = sourceFiles.Length, packageCount = packages, independentlyOpened = verifiedFiles,
    referenceCount = verifiedReferences, sameNameCollisionVerified = collisionVerified,
    sourceHashesUnchanged = true, vaultHashesUnchanged = true, databaseBomUnchanged = true,
    sourceSha256 = sourceHashes.Values.Order().ToArray(), adapter = "DocumentManager", fullSolidWorksGeometryRebuild = false,
    separatePhysicalWorkstation = false
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"NATIVE_PACKAGE_PASS sourceFiles={sourceFiles.Length}; packages={packages}; independentlyOpened={verifiedFiles}; references={verifiedReferences}; collision/relocation/manifest/sourceHashes/vaultHashes/databaseBom=PASS");

static int IndependentOpen(string path, string destination, JsonElement manifestEntry, string key)
{
    var factory = new SwDMClassFactory(); SwDMApplication4? app = null; SwDMDocument18? doc = null; SwDMSearchOption? search = null;
    try
    {
        app = (SwDMApplication4)factory.GetApplication(key);
        var kind = Path.GetExtension(path).ToLowerInvariant() switch { ".sldasm" => SwDmDocumentType.swDmDocumentAssembly, ".slddrw" => SwDmDocumentType.swDmDocumentDrawing, _ => SwDmDocumentType.swDmDocumentPart };
        doc = (SwDMDocument18)app.GetDocument(path, kind, true, out var error);
        Check(doc != null && error == SwDmDocumentOpenError.swDmDocumentOpenErrorNone, "Independent native reopen failed.");
        search = app.GetSearchOptionObject(); search.SearchFilters = (int)(SwDmSearchFilters.SwDmSearchExternalReference | SwDmSearchFilters.SwDmSearchForPart |
            SwDmSearchFilters.SwDmSearchForAssembly | SwDmSearchFilters.SwDmSearchInContextReference | SwDmSearchFilters.SwDmSearchRootAssemblyFolder);
        search.AddSearchPath(destination);
        var references = Strings(doc!.GetAllExternalReferences4(search, out _, out object virtuals, out _));
        var expected = manifestEntry.GetProperty("dependencies").EnumerateArray().Select(d => d.GetProperty("fileName").GetString()!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < references.Length; i++)
        {
            if (virtuals is Array flags && i < flags.Length && Convert.ToBoolean(flags.GetValue(i + flags.GetLowerBound(0)))) continue;
            string full = Path.GetFullPath(references[i]);
            Check(full.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(full), "Independent native reference escaped extracted package.");
            actual.Add(Path.GetFileName(full));
        }
        Check(expected.SetEquals(actual), "Independent native targets differ from selected manifest.");
        return actual.Count;
    }
    finally { doc?.CloseDoc(); Release(search); Release(doc); Release(app); Release(factory); }
}
static void ProbeCache(string path, string key)
{
    var factory = new SwDMClassFactory(); SwDMApplication4? app = null; SwDMDocument18? doc = null; SwDMConfigurationMgr? manager = null;
    try
    {
        app = (SwDMApplication4)factory.GetApplication(key);
        doc = (SwDMDocument18)app.GetDocument(path, Path.GetExtension(path).ToLowerInvariant() == ".sldasm" ? SwDmDocumentType.swDmDocumentAssembly : SwDmDocumentType.swDmDocumentDrawing, true, out _);
        doc.GetChangedReferences(out object old, out object updated); Console.WriteLine("NATIVE_CHANGE_CACHE " + JsonSerializer.Serialize(new { old = Strings(old), updated = Strings(updated) }));
        if (Path.GetExtension(path).Equals(".sldasm", StringComparison.OrdinalIgnoreCase))
        {
            manager = doc.ConfigurationManager;
            foreach (string name in Strings(manager.GetConfigurationNames()))
            {
                var config = manager.GetConfigurationByName(name);
                try { ((ISwDMConfiguration11)config).GetReplacedComponents(out object names, out object configurations, out object paths, out _); Console.WriteLine("NATIVE_COMPONENT_CACHE " + JsonSerializer.Serialize(new { name, names = Strings(names), configurations = Strings(configurations), paths = Strings(paths) })); }
                finally { Release(config); }
            }
        }
    }
    finally { doc?.CloseDoc(); Release(manager); Release(doc); Release(app); Release(factory); }
}
static async Task<string> Snapshot(PdmDbContext db) => JsonSerializer.Serialize(new {
    Docs = await db.Documents.AsNoTracking().OrderBy(d => d.DocumentId).ToArrayAsync(),
    Versions = await db.DocumentVersions.AsNoTracking().OrderBy(d => d.VersionId).ToArrayAsync(),
    Bom = await db.BomOccurrences.AsNoTracking().OrderBy(d => d.BomOccurrenceId).ToArrayAsync()
});
static string[] Strings(object? value) => value is Array array ? array.Cast<object>().Select(x => x.ToString()!).ToArray() : [];
static bool Cad(string path) => Path.GetExtension(path).ToLowerInvariant() is ".sldprt" or ".sldasm" or ".slddrw";
static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static void Release(object? value) { if (OperatingSystem.IsWindows() && value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
#else
throw new PlatformNotSupportedException("Native package validation requires Document Manager.");
#endif

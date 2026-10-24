namespace SWPdm.Api.Services;

using System.Data;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SWPdm.Api.Configuration;
using SWPdm.Sample.Data;
using SWPdm.Sample.Data.Entities;
using SWPdm.Sample.Services;

public sealed record PackageIssue(string Code, long? VersionId, string FileName, string Message);
public sealed class PackageBuildException(string code, string message, IReadOnlyList<PackageIssue>? issues = null) : Exception(message)
{
    public string Code { get; } = code;
    public IReadOnlyList<PackageIssue> Issues { get; } = issues ?? [];
}

/// <summary>No database writes. Native operations and deletions are confined to newly created package copies.</summary>
public sealed class CadPackageService(PdmDbContext db, LocalStorageService storage, ICadPackageSessionFactory cad)
{
    public const int MaxFiles = 1000;
    public const long MaxBytes = 512L * 1024 * 1024;

    public async Task<byte[]> BuildAsync(long rootVersionId, bool useLatest = false, string[]? versionOverrides = null,
        bool includeDrawings = false, CancellationToken cancellationToken = default)
    {
        if (rootVersionId <= 0) throw new ArgumentException("請選擇有效的根圖檔版本。");
        var overrides = new Dictionary<long, long>();
        foreach (string value in versionOverrides ?? [])
        {
            var parts = value.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !long.TryParse(parts[0], out long source) || source <= 0 ||
                !long.TryParse(parts[1], out long target) || target <= 0 || !overrides.TryAdd(source, target))
                throw new ArgumentException("版本選擇格式錯誤或重複，請重新選擇後下載。");
        }
        if (useLatest && overrides.Count != 0) throw new ArgumentException("最新版本與指定版本不能同時使用。");
        string workspace = Path.Combine(Path.GetTempPath(), "swpdm_package_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true
                    ? IsolationLevel.Serializable : IsolationLevel.RepeatableRead, cancellationToken);
            if (db.Database.IsNpgsql()) await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
            var builder = new Builder(db, storage, cad, workspace, useLatest, overrides, includeDrawings, cancellationToken);
            return await builder.Build(rootVersionId);
            // Disposing rolls back the read-only transaction; never SaveChanges/Migrate/EnsureCreated here.
        }
        catch (OperationCanceledException) { throw; }
        catch (PackageBuildException) { throw; }
        catch (KeyNotFoundException) { throw; }
        catch (ArgumentException) { throw; }
        catch (Exception)
        {
            throw new PackageBuildException("PACKAGE_BUILD_FAILED", "套件建立失敗，未提供不完整的下載檔；請重試或聯絡維護人員。");
        }
        finally
        {
            // The exact GUID directory was created by this request, never a configured/user-supplied directory.
            try { if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true); }
            catch (Exception) { throw new PackageBuildException("PACKAGE_CLEANUP_FAILED", "套件暫存清理失敗，請聯絡維護人員；未提供下載檔。"); }
        }
    }

    private sealed class Node(PdmDocumentVersion version, string name)
    {
        public PdmDocumentVersion Version { get; } = version;
        public string Name { get; } = name;
        public List<(PdmBomOccurrence Row, long Target, string Source)> Bom { get; } = [];
        public List<Binding> Bindings { get; } = [];
        public CadPackageInspection? Inspection;
        public string SourceHash = "";
        public string OutputHash = "";
        public bool Expanded;
    }
    private sealed record Binding(string Original, long Target, string Configuration, string ParentConfiguration, CadReferencePurpose Purpose, bool? Suppressed);

    private sealed class Builder(PdmDbContext db, LocalStorageService storage, ICadPackageSessionFactory factory,
        string workspace, bool latest, Dictionary<long, long> overrides, bool drawings, CancellationToken ct)
    {
        private readonly Dictionary<long, PdmDocumentVersion> versions = [];
        private readonly Dictionary<long, Node> nodes = [];
        private readonly Dictionary<string, HashSet<long>> aliases = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<long> usedOverrides = [];
        private readonly HashSet<long> consideredDrawings = [];
        private long totalBytes;
        private string work = Path.Combine(workspace, "work");

        public async Task<byte[]> Build(long requestedRoot)
        {
            foreach (var item in overrides)
                if ((await Version(item.Key)).DocumentId != (await Version(item.Value)).DocumentId)
                    throw new ArgumentException("指定版本不屬於同一份圖檔，未建立下載套件。");
            long selectedRoot = await Add(requestedRoot);
            await Expand();
            Directory.CreateDirectory(work);
            using var session = OpenSession();
            while (nodes.Values.Any(n => n.Inspection == null))
            {
                foreach (var node in nodes.Values.Where(n => n.Inspection == null).ToArray())
                {
                    ct.ThrowIfCancellationRequested();
                    await Copy(node);
                    node.Inspection = Inspect(session, node);
                    if (node.Inspection.Kind.ToString() != node.Version.Document.DocumentType)
                        throw Error("PACKAGE_TYPE_MISMATCH", "CAD檔案類型與版本紀錄不符。", node);
                    foreach (var reference in node.Inspection.References.Where(r => r.IsVirtual != true))
                    {
                        if (!IsCad(reference.Path)) throw Error("PACKAGE_UNSUPPORTED_REFERENCE", "包含不支援的外部格式，不能保證完整套件。", node);
                        string path = Normalize(reference.Path, node.Version.SourceFilePath);
                        var evidencePaths = node.Inspection.ReferenceAliases.Where(a => Normalize(a.Value, node.Version.SourceFilePath).Equals(path, StringComparison.OrdinalIgnoreCase))
                            .Select(a => Normalize(a.Key, node.Version.SourceFilePath)).Append(path).ToHashSet(StringComparer.OrdinalIgnoreCase);
                        var local = node.Bom.Where(b => evidencePaths.Contains(b.Source)).Select(b => b.Target).Distinct().ToArray();
                        long target;
                        if (local.Length > 1) throw Error("PACKAGE_REFERENCE_AMBIGUOUS", "同一CAD參照對應多個版本，請人工確認。", node);
                        if (local.Length == 1) target = local[0];
                        else if (evidencePaths.Any(p => aliases.ContainsKey(p)))
                        {
                            var candidates = evidencePaths.Where(p => aliases.ContainsKey(p)).SelectMany(p => aliases[p]).Distinct().ToArray();
                            if (candidates.Length != 1) throw Error("PACKAGE_REFERENCE_AMBIGUOUS", "參照版本有歧義，未依同名檔猜測。", node);
                            target = candidates.Single();
                        }
                        else
                        {
                            string[] normalized = evidencePaths.Select(p => p.Replace('\\', '/').ToLowerInvariant()).ToArray();
                            var candidatesByPath = await db.DocumentVersions.AsNoTracking().Include(v => v.Document)
                                .Where(v => normalized.Contains(v.SourceFilePath.Replace("\\", "/").ToLower())).Take(MaxFiles + 1).ToListAsync(ct);
                            if (candidatesByPath.Count == 0)
                                throw Error("PACKAGE_REFERENCE_MISSING", "CAD有尚未明確入庫的外部依賴，請先確認來源與版本。", node);
                            var explicitCandidates = candidatesByPath.Where(v => overrides.ContainsKey(v.VersionId)).ToArray();
                            if (candidatesByPath.Count == 1) target = await Add(candidatesByPath[0].VersionId);
                            else if (explicitCandidates.Length == 1) target = await Add(explicitCandidates[0].VersionId);
                            else if (latest && candidatesByPath.Select(v => v.DocumentId).Distinct().Count() == 1)
                                target = await Add(candidatesByPath[0].VersionId);
                            else throw Error("PACKAGE_REFERENCE_AMBIGUOUS", "外部依賴有多個候選版本，請先明確選版。", node);
                            Alias(path, target);
                            await Expand();
                        }
                        if (target == node.Version.VersionId) throw Error("PACKAGE_REFERENCE_CYCLE", "套件參照出現循環，未建立ZIP。", node);
                        node.Bindings.Add(new(reference.Path, target, reference.ChildConfiguration, reference.ParentConfiguration, reference.Purpose, reference.IsSuppressed));
                    }
                    foreach (var link in node.Bom)
                        if (!node.Bindings.Any(b => b.Target == link.Target &&
                            (Normalize(b.Original, node.Version.SourceFilePath).Equals(link.Source, StringComparison.OrdinalIgnoreCase) ||
                             node.Inspection.ReferenceAliases.Any(a => Normalize(a.Key, node.Version.SourceFilePath).Equals(link.Source, StringComparison.OrdinalIgnoreCase) &&
                                Normalize(a.Value, node.Version.SourceFilePath).Equals(Normalize(b.Original, node.Version.SourceFilePath), StringComparison.OrdinalIgnoreCase)))))
                            throw Error("PACKAGE_BOM_MISMATCH", "版本BOM與實際CAD參照不一致，請先確認後再下載。", node);
                }
            }
            if (usedOverrides.Count != overrides.Count) throw new ArgumentException("部分指定版本已不在選定套件的依賴圖，請重新檢查選版。");
            var ordered = Order();
            foreach (var node in ordered)
            {
                var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var binding in node.Bindings)
                {
                    var target = nodes[binding.Target];
                    if (!string.IsNullOrWhiteSpace(binding.Configuration) && !target.Inspection!.Configurations.Contains(binding.Configuration, StringComparer.OrdinalIgnoreCase))
                        throw Error("PACKAGE_CONFIGURATION_MISSING", "選定子文件沒有父文件要求的組態，請重新選版。", node);
                    string destination = Path.Combine(work, target.Name);
                    if (replacements.TryGetValue(binding.Original, out var prior) && prior != destination)
                        throw Error("PACKAGE_REFERENCE_AMBIGUOUS", "同一內部參照不能替換為兩個不同版本。", node);
                    replacements[binding.Original] = destination;
                }
                if (replacements.Count != 0)
                {
                    ct.ThrowIfCancellationRequested();
                    try { session.Rewrite(Path.Combine(work, node.Name), replacements); }
                    catch (Exception error) when (error is not OperationCanceledException)
                    { throw Error("PACKAGE_REWRITE_FAILED", "副本參照重寫或儲存失敗，未提供不完整套件。", node); }
                }
            }
            // Remove the original working location before validation; stale staging paths must not be enough to pass.
            string relocated = Path.Combine(workspace, "relocated");
            Directory.Move(work, relocated); work = relocated;
            long outputBytes = 0;
            foreach (var node in nodes.Values)
            {
                var reopened = Inspect(session, node);
                if (reopened.Kind != node.Inspection!.Kind ||
                    !reopened.Configurations.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(node.Inspection.Configurations) ||
                    reopened.References.Count(r => r.IsVirtual == true) != node.Inspection.References.Count(r => r.IsVirtual == true))
                    throw Error("PACKAGE_RELOCATION_FAILED", "重寫後圖檔類型、組態或虛擬件證據改變，未提供套件。", node);
                var expected = node.Bindings.Select(b => nodes[b.Target].Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var reference in reopened.References.Where(r => r.IsVirtual != true))
                {
                    string full = Path.GetFullPath(reference.Path, work);
                    if (!full.StartsWith(work + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                        !File.Exists(full) || !expected.Contains(Path.GetFileName(full)))
                        throw Error("PACKAGE_RELOCATION_FAILED", "搬移後仍有套件外參照或錯誤目標，未產生ZIP。", node);
                    actual.Add(Path.GetFileName(full));
                }
                if (!expected.SetEquals(actual)) throw Error("PACKAGE_RELOCATION_FAILED", "搬移後參照數量或目標不符，未產生ZIP。", node);
                foreach (var binding in node.Bindings.Where(b => !string.IsNullOrWhiteSpace(b.ParentConfiguration)))
                    if (!reopened.References.Any(r => Path.GetFileName(r.Path).Equals(nodes[binding.Target].Name, StringComparison.OrdinalIgnoreCase) &&
                        r.ParentConfiguration.Equals(binding.ParentConfiguration, StringComparison.OrdinalIgnoreCase) &&
                        r.ChildConfiguration.Equals(binding.Configuration, StringComparison.OrdinalIgnoreCase) &&
                        (!binding.Suppressed.HasValue || r.IsSuppressed == binding.Suppressed)))
                        throw Error("PACKAGE_RELOCATION_FAILED", "搬移後元件的父／子組態或抑制狀態不符，未提供套件。", node);
                long length = new FileInfo(Path.Combine(work, node.Name)).Length;
                if (length > MaxBytes - outputBytes) throw Error("PACKAGE_LIMIT", "重寫後套件超過512MiB，未提供下載檔。", node);
                outputBytes += length;
                node.OutputHash = await Hash(Path.Combine(work, node.Name));
            }
            var manifest = new
            {
                schemaVersion = 1, requestedRootVersionId = requestedRoot, selectedRootVersionId = selectedRoot,
                rootFile = nodes[selectedRoot].Name, useLatest = latest, includeDrawings = drawings,
                files = nodes.Values.OrderBy(n => n.Version.VersionId).Select(n => new
                {
                    n.Version.DocumentId, n.Version.VersionId, n.Version.VersionNo, n.Version.RevisionLabel,
                    documentType = n.Version.Document.DocumentType, fileName = n.Name,
                    sourceSha256 = n.SourceHash, packageSha256 = n.OutputHash,
                    dependencies = n.Bindings.Select(b => new { versionId = b.Target, fileName = nodes[b.Target].Name,
                        configuration = b.Configuration, parentConfiguration = b.ParentConfiguration, purpose = b.Purpose.ToString(), suppressed = b.Suppressed }).Distinct(),
                    virtualReferenceCount = n.Inspection!.References.Count(r => r.IsVirtual == true)
                })
            };
            await using var buffer = new MemoryStream();
            using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var node in nodes.Values.OrderBy(n => n.Name, StringComparer.Ordinal))
                {
                    ct.ThrowIfCancellationRequested();
                    var entry = zip.CreateEntry(node.Name, CompressionLevel.Fastest);
                    await using var output = entry.Open(); await using var input = File.OpenRead(Path.Combine(work, node.Name));
                    await input.CopyToAsync(output, ct);
                }
                var manifestEntry = zip.CreateEntry("manifest.json");
                await using var stream = manifestEntry.Open();
                await JsonSerializer.SerializeAsync(stream, manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web), ct);
            }
            if (buffer.Length > MaxBytes + 1024 * 1024) throw Error("PACKAGE_LIMIT", "套件超過單次下載限制，請縮小範圍。");
            return buffer.ToArray();
        }

        private ICadPackageSession OpenSession()
        {
            try { return factory.Open(workspace); }
            catch (Exception) { throw new PackageBuildException("PACKAGE_NATIVE_UNAVAILABLE", "CAD完整性檢查服務不可用，暫時無法產生套件。"); }
        }
        private CadPackageInspection Inspect(ICadPackageSession session, Node node)
        {
            ct.ThrowIfCancellationRequested();
            try { return session.Inspect(Path.Combine(work, node.Name)); }
            catch (Exception error) when (error is not OperationCanceledException)
            { throw Error("PACKAGE_NATIVE_FAILED", "無法完整讀取CAD副本的所有參照，未產生套件。", node); }
        }
        private async Task<PdmDocumentVersion> Version(long id)
        {
            if (versions.TryGetValue(id, out var found)) return found;
            var version = await db.DocumentVersions.AsNoTracking().Include(v => v.Document).SingleOrDefaultAsync(v => v.VersionId == id, ct)
                ?? throw new KeyNotFoundException("找不到指定的圖檔版本，請重新整理。");
            versions[id] = version; return version;
        }
        private async Task<long> Add(long source)
        {
            var original = await Version(source);
            long target = source;
            if (overrides.TryGetValue(source, out long selected)) { usedOverrides.Add(source); target = selected; }
            else if (latest) target = original.Document.CurrentVersionId ?? throw Error("PACKAGE_CURRENT_VERSION_MISSING", "圖檔缺少目前版本，無法使用最新版。");
            var version = await Version(target);
            if (version.DocumentId != original.DocumentId) throw new ArgumentException("指定版本不屬於同一份圖檔。");
            if (!nodes.ContainsKey(target))
            {
                if (nodes.Count >= MaxFiles) throw Error("PACKAGE_LIMIT", "套件超過1000個檔案，請縮小範圍。");
                nodes[target] = new(version, Name(version));
            }
            if (!string.IsNullOrWhiteSpace(original.SourceFilePath)) Alias(Normalize(original.SourceFilePath), target);
            if (!string.IsNullOrWhiteSpace(version.SourceFilePath)) Alias(Normalize(version.SourceFilePath), target);
            return target;
        }
        private async Task Expand()
        {
            bool repeat;
            do
            {
                while (nodes.Values.FirstOrDefault(n => !n.Expanded) is { } node)
                {
                    node.Expanded = true;
                    var rows = await db.BomOccurrences.AsNoTracking().Where(b => b.ParentVersionId == node.Version.VersionId).ToListAsync(ct);
                    foreach (var row in rows.Where(r => !r.IsVirtual))
                    {
                        if (row.ChildVersionId == null || row.ReferenceStatus != "Resolved")
                            throw Error("PACKAGE_BOM_UNRESOLVED", "所選版本仍有未解析或缺失的BOM，請先確認。", node);
                        if (string.IsNullOrWhiteSpace(row.SourceReferencePath)) throw Error("PACKAGE_BOM_MISMATCH", "BOM缺少原始參照證據。", node);
                        long target = await Add(row.ChildVersionId.Value);
                        string source = Normalize(row.SourceReferencePath, node.Version.SourceFilePath);
                        node.Bom.Add((row, target, source)); Alias(source, target);
                    }
                }
                repeat = false;
                if (drawings)
                {
                    long[] ids = nodes.Values.Where(n => n.Version.Document.DocumentType != "Drawing").Select(n => n.Version.DocumentId).Distinct().ToArray();
                    var linked = await db.BomOccurrences.AsNoTracking().Where(b => b.ChildVersionId != null &&
                        ids.Contains(b.ChildVersion!.DocumentId) && b.ParentVersion.Document.DocumentType == "Drawing")
                        .Select(b => b.ParentVersion.DocumentId).Distinct().ToArrayAsync(ct);
                    foreach (long id in linked.Where(id => consideredDrawings.Add(id)))
                    {
                        var document = await db.Documents.AsNoTracking().SingleAsync(d => d.DocumentId == id, ct);
                        if (document.CurrentVersionId == null) throw Error("PACKAGE_CURRENT_VERSION_MISSING", "關聯工程圖沒有目前版本。");
                        await Add(document.CurrentVersionId.Value); repeat = true;
                    }
                    // Filename fallback is discovery only. Native paths/version evidence still have to pass full validation.
                    var missing = await db.BomOccurrences.AsNoTracking().Where(b => b.ChildVersionId == null && b.ParentVersion.Document.DocumentType == "Drawing")
                        .Select(b => new { b.SourceReferencePath, b.ParentVersion.DocumentId }).ToListAsync(ct);
                    var names = nodes.Values.Where(n => n.Version.Document.DocumentType != "Drawing").Select(n => Path.GetFileName(n.Version.OriginalFileName)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    foreach (long id in missing.Where(b => names.Contains(Path.GetFileName(b.SourceReferencePath))).Select(b => b.DocumentId).Distinct().Where(id => consideredDrawings.Add(id)))
                    {
                        var document = await db.Documents.AsNoTracking().SingleAsync(d => d.DocumentId == id, ct);
                        if (document.CurrentVersionId == null) throw Error("PACKAGE_CURRENT_VERSION_MISSING", "關聯工程圖沒有目前版本。");
                        await Add(document.CurrentVersionId.Value); repeat = true;
                    }
                }
            } while (repeat);
        }
        private async Task Copy(Node node)
        {
            var version = node.Version;
            if (version.ChecksumSha256 == null || version.ChecksumSha256.Length != 64 || !version.ChecksumSha256.All(Uri.IsHexDigit))
                throw Error("PACKAGE_CHECKSUM_MISSING", "版本缺少可靠的SHA256，請先確認檔案完整性。", node);
            try
            {
                string source = storage.GetPackageSourcePath(version.StorageFileId);
                await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
                if (input.Length > MaxBytes - totalBytes) throw Error("PACKAGE_LIMIT", "套件超過512MiB，請縮小範圍。", node);
                if (version.FileSizeBytes.HasValue && version.FileSizeBytes != input.Length)
                    throw Error("PACKAGE_CHECKSUM_MISMATCH", "實體檔案大小與版本紀錄不符。", node);
                totalBytes += input.Length;
                await using (var output = new FileStream(Path.Combine(work, node.Name), FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                    await input.CopyToAsync(output, ct);
                node.SourceHash = await Hash(Path.Combine(work, node.Name));
                if (!node.SourceHash.Equals(version.ChecksumSha256, StringComparison.OrdinalIgnoreCase))
                    throw Error("PACKAGE_CHECKSUM_MISMATCH", "實體檔案SHA256與版本紀錄不符，未提供套件。", node);
            }
            catch (PackageBuildException) { throw; }
            catch (OperationCanceledException) { throw; }
            catch (ArgumentException) { throw Error("PACKAGE_STORAGE_UNSAFE", "版本的儲存路徑不安全，請聯絡維護人員。", node); }
            catch (Exception) { throw Error("PACKAGE_FILE_MISSING", "找不到或無法唯讀取得所需圖檔，未提供不完整套件。", node); }
        }
        private List<Node> Order()
        {
            var visited = new HashSet<long>(); var active = new HashSet<long>(); var result = new List<Node>();
            void Visit(Node node)
            {
                if (active.Contains(node.Version.VersionId)) throw Error("PACKAGE_REFERENCE_CYCLE", "套件依賴有循環，請先確認CAD關係。", node);
                if (!visited.Add(node.Version.VersionId)) return;
                active.Add(node.Version.VersionId);
                foreach (long target in node.Bindings.Select(b => b.Target).Distinct()) Visit(nodes[target]);
                active.Remove(node.Version.VersionId); result.Add(node);
            }
            foreach (var node in nodes.Values) Visit(node);
            return result;
        }
        private void Alias(string path, long target) { if (!aliases.TryGetValue(path, out var values)) aliases[path] = values = []; values.Add(target); }
        private static bool IsCad(string path) => Path.GetExtension(path).ToLowerInvariant() is ".sldprt" or ".sldasm" or ".slddrw";
        private static string Normalize(string path, string parent = "")
        {
            try { return Path.GetFullPath(path.Replace('\\', Path.DirectorySeparatorChar),
                string.IsNullOrWhiteSpace(parent) ? Path.GetTempPath() : Path.GetDirectoryName(Path.GetFullPath(parent))!).TrimEnd(Path.DirectorySeparatorChar); }
            catch (Exception) { throw Error("PACKAGE_REFERENCE_PATH_INVALID", "參照路徑格式不安全或無法判定，請先確認CAD來源。"); }
        }
        private static string Name(PdmDocumentVersion version)
        {
            string extension = version.Document.DocumentType switch { "Part" => ".sldprt", "Assembly" => ".sldasm", "Drawing" => ".slddrw", _ => "" };
            if (extension == "" || !Path.GetExtension(version.OriginalFileName).Equals(extension, StringComparison.OrdinalIgnoreCase))
                throw Error("PACKAGE_TYPE_MISMATCH", "檔名與圖檔類型不符，不能安全建立套件。");
            string name = Path.GetFileNameWithoutExtension(version.OriginalFileName.Replace('\\', '/'));
            name = new string(name.Select(c => char.IsControl(c) || "<>:\"/\\|?*".Contains(c) ? '_' : c).ToArray());
            if (name.Length > 100) name = name[..100];
            return $"v{version.VersionId}__{name}{extension}";
        }
        private async Task<string> Hash(string path) { await using var stream = File.OpenRead(path); return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant(); }
        private static PackageBuildException Error(string code, string message, Node? node = null) => new(code, message,
            [new(code, node?.Version.VersionId, node?.Name ?? "", message)]);
    }
}

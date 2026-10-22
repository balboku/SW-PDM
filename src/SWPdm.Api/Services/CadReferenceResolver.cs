namespace SWPdm.Api.Services;

using System.Security.Cryptography;
using SWPdm.Api.Configuration;
using Microsoft.EntityFrameworkCore;
using SWPdm.Sample.Data;
using SWPdm.Sample.Data.Entities;
using SWPdm.Sample.Services;

public sealed record CadReferenceMatch(long? VersionId, string Status, string Method, string? Issue);

public sealed class CadReferenceResolver(PdmDbContext db)
{
    public async Task<CadReferenceMatch> ResolveAsync(string path, ICadIngestionSession cad,
        IReadOnlyCollection<string> searchPaths, CancellationToken ct)
    {
        string name = Path.GetFileName(path);
        // Prefer the staged file at the exact path; only use a search-folder match when unique.
        var candidates = File.Exists(path) ? new[] { path } : searchPaths
            .Select(p => Path.Combine(p, name)).Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (candidates.Length > 1) return Broken(name, "批次內有多個同名檔案，請確認原始參照路徑。");
        if (candidates.Length == 1)
        {
            var parsed = cad.Parse(candidates[0], searchPaths);
            var identity = CadPropertyResolver.Resolve(parsed, CadPropertyResolver.PartNumberNames);
            if (identity.Value == null) return Broken(name, "參照檔案的品號缺少或衝突。");
            string number = identity.Value.ToUpperInvariant();
            string kind = parsed.DocumentType.ToString();
            await using var stream = File.OpenRead(candidates[0]);
            string hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
            var versions = await db.DocumentVersions.AsNoTracking().Where(v =>
                v.Document.DocumentType == kind && v.Document.PartNumber != null &&
                v.Document.PartNumber.Trim().ToUpper() == number && v.ChecksumSha256 == hash).ToListAsync(ct);
            if (versions.Select(v => v.DocumentId).Distinct().Count() > 1)
                return Broken(name, "多個文件具有相同品號與內容，請先處理重複身分。");
            if (versions.Count > 1)
                return Broken(name, "相同身分與內容對應多個歷史版本，不能自動選最新；請確認需引用的版本。");
            var version = versions.SingleOrDefault();
            return version == null
                ? new(null, "Missing", "IdentityHash", $"{name} 的實際內容尚未入庫；請先入庫此版本。")
                : new(version.VersionId, "Resolved", "IdentityHash", null);
        }

        string lowerName = name.ToLowerInvariant();
        var matches = await db.DocumentVersions.AsNoTracking()
            .Where(v => v.OriginalFileName.ToLower() == lowerName).ToListAsync(ct);
        if (matches.Count == 0) return new(null, "Missing", "None", $"缺少參照 {name}；請上傳完整資料夾。");
        if (matches.Select(v => v.DocumentId).Distinct().Count() != 1 ||
            matches.Any(v => string.IsNullOrWhiteSpace(v.ChecksumSha256)) ||
            matches.Select(v => v.ChecksumSha256).Distinct().Count() != 1)
            return Broken(name, "同檔名對應多個文件或不同內容版本；請提供原始參照檔案以確認。");
        return new(null, "Missing", "FilenameCandidateOnly",
            $"{name} 只有檔名候選，沒有原始內容可確認版本；請提供實際參照檔案。");
    }

    private static CadReferenceMatch Broken(string name, string message) => new(null, "Broken", "Ambiguous", $"{name}：{message}");
}

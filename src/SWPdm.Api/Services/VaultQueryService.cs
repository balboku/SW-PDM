namespace SWPdm.Api.Services;

using System.Data;
using Microsoft.EntityFrameworkCore;
using SWPdm.Sample.Data;
using SWPdm.Sample.Data.Entities;

/// <summary>Stored snapshot evidence only: never opens a CAD file, repairs a BOM or writes data.</summary>
public sealed class VaultQueryService(PdmDbContext db)
{
    public async Task<object> SearchAsync(string? text, string? type, string? status, string? integrity,
        bool paged, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (text?.Length ?? 0) > 200)
            throw new ArgumentException("請使用有效頁碼、每頁1～100筆及200字內的搜尋條件。");
        type = string.IsNullOrWhiteSpace(type) ? "All" : type;
        status = string.IsNullOrWhiteSpace(status) ? "All" : status;
        integrity = string.IsNullOrWhiteSpace(integrity) ? "All" : integrity;
        if (type is not ("All" or "Part" or "Assembly" or "Drawing") ||
            status is not ("All" or "Available" or "CheckedOut") || integrity is not ("All" or "Missing" or "Newer"))
            throw new ArgumentException("篩選條件不正確，請重新選擇。");
        await using var transaction = await ReadTransaction(ct);
        var docs = db.Documents.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(text))
        {
            string term = text.Trim().ToLowerInvariant();
            docs = docs.Where(d => d.FileName.ToLower().Contains(term) ||
                (d.PartNumber != null && d.PartNumber.ToLower().Contains(term)) ||
                db.CustomProperties.Any(p => p.VersionId == d.CurrentVersionId &&
                    p.PropertyValue != null && p.PropertyValue.ToLower().Contains(term)));
        }
        if (type != "All") docs = docs.Where(d => d.DocumentType == type);
        if (status == "CheckedOut") docs = docs.Where(d => d.CheckedOutBy != null);
        if (status == "Available") docs = docs.Where(d => d.CheckedOutBy == null);
        var query = docs.Select(d => new VaultRow {
            DocumentId = d.DocumentId, FileName = d.FileName, PartNumber = d.PartNumber, DocumentType = d.DocumentType,
            RevisionLabel = d.CurrentVersion != null ? d.CurrentVersion.RevisionLabel ?? d.RevisionLabel : d.RevisionLabel,
            HasVersionRevision = d.CurrentVersion != null && d.CurrentVersion.RevisionLabel != null,
            Material = d.Material, CurrentVersionId = d.CurrentVersionId,
            CurrentVersionNo = d.CurrentVersion != null ? d.CurrentVersion.VersionNo : null,
            CurrentLifecycleState = d.CurrentVersion != null ? d.CurrentVersion.LifecycleState : null,
            UpdatedAt = d.UpdatedAt, CheckedOutBy = d.CheckedOutBy, CheckedOutAt = d.CheckedOutAt,
            MissingReferenceCount = (d.CurrentVersionId == null ? 1 : 0) + db.BomOccurrences.Count(b => b.ParentVersionId == d.CurrentVersionId &&
                !b.IsVirtual && (b.ChildVersionId == null || b.ReferenceStatus != "Resolved")),
            ReferenceUpdateCount = db.BomOccurrences.Where(b => b.ParentVersionId == d.CurrentVersionId && !b.IsVirtual && b.ReferenceStatus == "Resolved" &&
                b.ChildVersionId != null && b.ChildVersion!.Document.CurrentVersion != null &&
                b.ChildVersion.Document.CurrentVersion.VersionNo > b.ChildVersion.VersionNo).Select(b => b.ChildVersion!.DocumentId).Distinct().Count(),
            AffectedReferenceOccurrenceCount = db.BomOccurrences.Count(b => b.ParentVersionId == d.CurrentVersionId && !b.IsVirtual && b.ReferenceStatus == "Resolved" &&
                b.ChildVersionId != null && b.ChildVersion!.Document.CurrentVersion != null && b.ChildVersion.Document.CurrentVersion.VersionNo > b.ChildVersion.VersionNo)
        });
        if (integrity == "Missing") query = query.Where(d => d.MissingReferenceCount > 0);
        if (integrity == "Newer") query = query.Where(d => d.ReferenceUpdateCount > 0);
        int total = await query.CountAsync(ct);
        int missing = await query.CountAsync(d => d.MissingReferenceCount > 0, ct);
        int newer = await query.CountAsync(d => d.ReferenceUpdateCount > 0, ct);
        int checkedOut = await query.CountAsync(d => d.CheckedOutBy != null, ct);
        int size = paged ? pageSize : 50;
        int totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)size));
        int selectedPage = paged ? Math.Min(page, totalPages) : 1;
        var items = await query.OrderByDescending(d => d.UpdatedAt).ThenBy(d => d.DocumentId)
            .Skip((selectedPage - 1) * size).Take(size).ToArrayAsync(ct);
        long[] ids = items.Where(i => i.CurrentVersionId.HasValue).Select(i => i.CurrentVersionId!.Value).ToArray();
        var properties = await db.CustomProperties.AsNoTracking().Where(p => ids.Contains(p.VersionId))
            .OrderBy(p => p.CustomPropertyId).ToArrayAsync(ct);
        foreach (var item in items)
        {
            var own = properties.Where(p => p.VersionId == item.CurrentVersionId).ToArray();
            item.PropertySources = Sources(own);
            item.PartName = item.PropertySources["partName"].Value;
            item.DrawingNumber = item.PropertySources["drawingNumber"].Value;
            if (!item.HasVersionRevision && item.PropertySources["revision"].Value is string revision)
                item.RevisionLabel = revision;
        }
        if (!paged) return items;
        return new { items, page = selectedPage, pageSize = size, totalCount = total, totalPages,
            newerReferenceDocumentCount = newer, missingReferenceDocumentCount = missing,
            checkedOutCount = checkedOut, availableCount = total - checkedOut, evidenceLevel = "StoredSnapshot", nativeFileChecked = false };
    }

    public async Task<object> IntegrityAsync(long id, CancellationToken ct)
    {
        await using var transaction = await ReadTransaction(ct);
        var doc = await db.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.DocumentId == id, ct)
            ?? throw new KeyNotFoundException("找不到這份圖檔，請重新載入清單。");
        var properties = await db.CustomProperties.AsNoTracking().Where(p => p.VersionId == doc.CurrentVersionId)
            .OrderBy(p => p.ConfigurationName).ThenBy(p => p.PropertyName).ThenBy(p => p.CustomPropertyId).ToArrayAsync(ct);
        var rows = await db.BomOccurrences.AsNoTracking().Where(b => b.ParentVersionId == doc.CurrentVersionId)
            .Include(b => b.ChildVersion).ThenInclude(v => v!.Document).ThenInclude(d => d.CurrentVersion)
            .OrderBy(b => b.BomOccurrenceId).ToArrayAsync(ct);
        var references = rows.Select(b => new {
            b.BomOccurrenceId, fileName = Path.GetFileName(b.SourceReferencePath.Replace('\\', '/')),
            b.ParentConfigurationName, b.ChildConfigurationName, b.ReferenceStatus, b.IsVirtual, b.IsSuppressed, b.ChildVersionId,
            childDocumentId = b.ChildVersion?.DocumentId, referencedVersionNo = b.ChildVersion?.VersionNo,
            currentVersionNo = b.ChildVersion?.Document.CurrentVersion?.VersionNo,
            needsResolution = !b.IsVirtual && (b.ChildVersionId == null || b.ReferenceStatus != "Resolved"),
            hasNewerVersion = !b.IsVirtual && b.ReferenceStatus == "Resolved" && b.ChildVersion?.Document.CurrentVersion?.VersionNo > b.ChildVersion?.VersionNo
        }).ToArray();
        return new { doc.DocumentId, doc.CurrentVersionId, doc.CheckedOutBy, doc.CheckedOutAt, evidenceLevel = "StoredSnapshot", nativeFileChecked = false,
            currentVersionMissing = doc.CurrentVersionId == null, unresolvedCount = references.Count(r => r.needsResolution),
            virtualCount = rows.Count(r => r.IsVirtual), suppressedCount = rows.Count(r => r.IsSuppressed),
            propertySources = Sources(properties),
            properties = properties.Select(p => new { p.ConfigurationName, p.PropertyName, p.PropertyValue, p.IsResolved }), references };
    }

    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> ReadTransaction(CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true
            ? IsolationLevel.Serializable : IsolationLevel.RepeatableRead, ct);
        if (db.Database.IsNpgsql()) await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", ct);
        return transaction;
    }
    private static Dictionary<string, PropertyEvidence> Sources(IEnumerable<PdmCustomProperty> properties) => new() {
        ["partName"] = Resolve(properties, ["品名", "PartName", "Description"]),
        ["drawingNumber"] = Resolve(properties, ["圖號"]),
        ["partNumber"] = Resolve(properties, ["品號", "PartNumber"]),
        ["revision"] = Resolve(properties, ["版次", "Revision", "Rev"])
    };
    private static PropertyEvidence Resolve(IEnumerable<PdmCustomProperty> properties, string[] names)
    {
        var values = properties.Where(p => p.ConfigurationName == "" && names.Contains(p.PropertyName) && !string.IsNullOrWhiteSpace(p.PropertyValue))
            .OrderBy(p => Array.IndexOf(names, p.PropertyName)).ThenBy(p => p.CustomPropertyId).ToArray();
        var distinct = values.Select(p => p.PropertyValue!).Distinct(StringComparer.Ordinal).ToArray();
        return new(distinct.Length == 1 ? distinct[0] : null, values.Length == 0 ? null : values[0].PropertyName,
            distinct.Length > 1 ? distinct : []);
    }
}

public sealed record PropertyEvidence(string? Value, string? Source, string[] Conflicts);
public sealed class VaultRow
{
    public long DocumentId { get; set; }
    public string FileName { get; set; } = "";
    public string? PartNumber { get; set; }
    public string DocumentType { get; set; } = "";
    public string? RevisionLabel { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasVersionRevision { get; set; }
    public string? Material { get; set; }
    public long? CurrentVersionId { get; set; }
    public int? CurrentVersionNo { get; set; }
    public string? CurrentLifecycleState { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? CheckedOutBy { get; set; }
    public DateTimeOffset? CheckedOutAt { get; set; }
    public int MissingReferenceCount { get; set; }
    public int ReferenceUpdateCount { get; set; }
    public int AffectedReferenceOccurrenceCount { get; set; }
    public string? PartName { get; set; }
    public string? DrawingNumber { get; set; }
    public Dictionary<string, PropertyEvidence> PropertySources { get; set; } = [];
}

namespace SWPdm.Api.Services;

using System.Data;
using Microsoft.EntityFrameworkCore;
using SWPdm.Sample.Data;
using SWPdm.Sample.Data.Entities;
using SWPdm.Sample.Data.Repositories;

public sealed record BatchRestoreItem(long DocumentId, long VersionId, DateTimeOffset LockTime,
    string FileName, bool CanRestore, string Code, string Reason);

public sealed class CheckoutBatchService(PdmDbContext db, IPdmRepository repository)
{
    public async Task<object> CheckOutAsync(long documentId, string actor, bool includeRelations, CancellationToken ct)
    {
        actor = CancelCheckoutService.NormalizeActor(actor);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var root = await db.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.DocumentId == documentId, ct)
            ?? throw new KeyNotFoundException("找不到出庫圖檔。");
        if (root.CheckedOutBy != null || root.CheckedOutAt.HasValue)
            throw new CancelCheckoutConflictException("根圖檔已出庫或鎖定狀態不完整，未取得任何鎖；請重新整理。");
        if (!root.CurrentVersionId.HasValue)
            throw new CancelCheckoutConflictException("圖檔缺少目前版本，未取得任何鎖。");
        var ids = await ScopeAsync(root, includeRelations, ct);
        var documents = await db.Documents.AsNoTracking().Where(d => ids.Contains(d.DocumentId)).OrderBy(d => d.DocumentId).ToListAsync(ct);
        if (documents.Any(d => !d.CurrentVersionId.HasValue ||
            (d.CheckedOutBy == null) != !d.CheckedOutAt.HasValue ||
            (d.CheckedOutBy != null && !string.Equals(d.CheckedOutBy, actor, StringComparison.OrdinalIgnoreCase))))
            throw new CancelCheckoutConflictException("關聯圖檔已由他人出庫或鎖／版本不完整；本批未取得任何鎖，請重新整理。");
        var now = MicrosecondNow();
        var batch = new PdmCheckoutBatch { BatchId = Guid.NewGuid(), RootDocumentId = documentId, Actor = actor, CreatedAt = now };
        foreach (var document in documents.Where(d => d.CheckedOutBy == null))
        {
            int changed = await db.Documents.Where(d => d.DocumentId == document.DocumentId &&
                d.CheckedOutBy == null && d.CheckedOutAt == null && d.CurrentVersionId == document.CurrentVersionId)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.CheckedOutBy, actor).SetProperty(d => d.CheckedOutAt, (DateTimeOffset?)now), ct);
            if (changed != 1) throw new CancelCheckoutConflictException("出庫狀態已改變，本批未完成；未保留任何新鎖，請重新整理。");
            batch.Items.Add(new() { DocumentId = document.DocumentId, VersionId = document.CurrentVersionId!.Value, LockTime = now });
        }
        db.CheckoutBatches.Add(batch);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new { message = "Checked out successfully", checkedOutBy = actor, batch.BatchId,
            acquiredCount = batch.Items.Count, reusedLockCount = documents.Count - batch.Items.Count };
    }

    public async Task<object> ListAsync(long documentId, string actor, CancellationToken ct)
    {
        actor = CancelCheckoutService.NormalizeActor(actor);
        string normalized = actor.ToLowerInvariant();
        return await db.CheckoutBatches.AsNoTracking().Where(b => b.RootDocumentId == documentId && b.Actor.ToLower() == normalized)
            .OrderByDescending(b => b.CreatedAt).ThenBy(b => b.BatchId)
            .Select(b => new { b.BatchId, b.RootDocumentId, b.Actor, b.CreatedAt, AcquiredCount = b.Items.Count,
                RestoredCount = b.Items.Count(i => i.RestoredAt.HasValue) }).ToArrayAsync(ct);
    }

    public async Task<object> PreviewAsync(long documentId, string actor, Guid batchId, CancellationToken ct)
    {
        actor = CancelCheckoutService.NormalizeActor(actor);
        var batch = await GetBatch(documentId, actor, batchId, ct);
        var ids = batch.Items.Select(i => i.DocumentId).ToArray();
        var docs = await db.Documents.AsNoTracking().Where(d => ids.Contains(d.DocumentId)).ToDictionaryAsync(d => d.DocumentId, ct);
        var items = batch.Items.OrderBy(i => i.DocumentId).Select(i => Evaluate(i, docs.GetValueOrDefault(i.DocumentId), actor)).ToArray();
        return new { Scope = "CheckoutBatch", batch.BatchId, batch.RootDocumentId, Actor = actor,
            CanRestore = items.Any(i => i.CanRestore), RestorableCount = items.Count(i => i.CanRestore), Items = items };
    }

    public async Task<object> UndoAsync(long documentId, UndoCheckoutRequest request, CancellationToken ct)
    {
        string actor = CancelCheckoutService.NormalizeActor(request.Actor);
        if (request.BatchId is null || request.BatchId == Guid.Empty)
            throw new ArgumentException("請先預覽並指定要復原的出庫批次；未解除任何鎖。");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var batch = await GetBatch(documentId, actor, request.BatchId.Value, ct);
        var ids = batch.Items.Select(i => i.DocumentId).ToArray();
        var docs = await db.Documents.AsNoTracking().Where(d => ids.Contains(d.DocumentId)).ToDictionaryAsync(d => d.DocumentId, ct);
        var restored = new List<long>();
        var skipped = new List<BatchRestoreItem>();
        foreach (var item in batch.Items.OrderBy(i => i.DocumentId))
        {
            var document = docs.GetValueOrDefault(item.DocumentId);
            var evaluation = Evaluate(item, document, actor);
            if (!evaluation.CanRestore) { skipped.Add(evaluation); continue; }
            // Match the observed owner exactly; concurrent changes must fail closed.
            int changed = await db.Documents.Where(d => d.DocumentId == item.DocumentId &&
                d.CheckedOutBy == document!.CheckedOutBy && d.CheckedOutAt == item.LockTime && d.CurrentVersionId == item.VersionId)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.CheckedOutBy, (string?)null).SetProperty(d => d.CheckedOutAt, (DateTimeOffset?)null), ct);
            if (changed != 1) throw new CancelCheckoutConflictException("復原期間鎖或版本已改變，本批未完成；請重新預覽，未保留部分解除。");
            int recorded = await db.CheckoutBatchItems.Where(i => i.BatchId == batch.BatchId && i.DocumentId == item.DocumentId && i.RestoredAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.RestoredAt, (DateTimeOffset?)MicrosecondNow()), ct);
            if (recorded != 1) throw new CancelCheckoutConflictException("批次紀錄已改變，本批未完成，請重新預覽。");
            restored.Add(item.DocumentId);
        }
        await transaction.CommitAsync(ct);
        return new { Scope = "CheckoutBatch", batch.BatchId, RestoredCount = restored.Count, RestoredDocumentIds = restored, Skipped = skipped };
    }

    private async Task<PdmCheckoutBatch> GetBatch(long rootId, string actor, Guid id, CancellationToken ct)
    {
        var batch = await db.CheckoutBatches.AsNoTracking().Include(b => b.Items)
            .SingleOrDefaultAsync(b => b.BatchId == id && b.RootDocumentId == rootId, ct)
            ?? throw new KeyNotFoundException("找不到指定根圖檔的出庫批次；請重新載入批次清單，不會改用其他批次。");
        if (!string.Equals(batch.Actor, actor, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("只能預覽及復原自己的出庫批次。");
        if (!batch.Items.Any(i => i.DocumentId == rootId))
            throw new CancelCheckoutConflictException("批次缺少根圖檔鎖定紀錄，未解除任何鎖，請人工確認。");
        return batch;
    }

    private static BatchRestoreItem Evaluate(PdmCheckoutBatchItem item, PdmDocument? document, string actor)
    {
        var (code, reason) = item.RestoredAt.HasValue ? ("AlreadyRestored", "此筆已復原，不會解除後續取得的新鎖。") :
            document == null ? ("DocumentMissing", "圖檔已不存在，保留原批次紀錄。") :
            document.CheckedOutBy == null ? ("Unlocked", "已入庫或已解鎖，不需再復原。") :
            !string.Equals(document.CheckedOutBy, actor, StringComparison.OrdinalIgnoreCase) ? ("OwnerChanged", "出庫人已改變，不會解除他人的鎖。") :
            document.CheckedOutAt != item.LockTime ? ("LockChanged", "已是其他出庫作業，不會解除新的鎖。") :
            document.CurrentVersionId != item.VersionId ? ("VersionChanged", "版本已更新，請單獨確認。") : ("Restorable", "可解除這次批次取得的鎖。");
        return new(item.DocumentId, item.VersionId, item.LockTime, document?.FileName ?? $"#{item.DocumentId}", code == "Restorable", code, reason);
    }

    private static DateTimeOffset MicrosecondNow()
    {
        var now = DateTimeOffset.UtcNow;
        return new(now.Ticks - now.Ticks % 10, TimeSpan.Zero);
    }

    // Follow selected BOM document identities, not a current-version join that can silently omit children.
    // Drawings still require their current version to match; never acquire where-used parent locks.
    private async Task<HashSet<long>> ScopeAsync(PdmDocument root, bool includeRelations, CancellationToken ct)
    {
        HashSet<long> ids = [root.DocumentId];
        if (!includeRelations) return ids;
        var children = await repository.GetPackageClosureAsync(root.CurrentVersionId!.Value, ct);
        var childIds = children.Select(c => c.DocumentId).Distinct().ToList();
        ids.UnionWith(childIds);
        var childNames = children.Select(c => c.OriginalFileName.ToLowerInvariant()).ToHashSet();
        var linked = await db.BomOccurrences.Where(b => b.ChildVersionId.HasValue &&
            childIds.Contains(b.ChildVersion!.DocumentId) && b.ParentVersion.Document.DocumentType == "Drawing")
            .Select(b => b.ParentVersionId).ToListAsync(ct);
        var missing = await db.BomOccurrences.Where(b => b.ChildVersionId == null && b.ParentVersion.Document.DocumentType == "Drawing")
            .Select(b => new { b.ParentVersionId, b.SourceReferencePath }).ToListAsync(ct);
        var versions = children.Select(c => c.VersionId).Concat(linked).Concat(missing
            .Where(b => !string.IsNullOrEmpty(b.SourceReferencePath) && childNames.Contains(Path.GetFileName(b.SourceReferencePath).ToLowerInvariant()))
            .Select(b => b.ParentVersionId)).Distinct().ToArray();
        ids.UnionWith(await db.Documents.Where(d => d.CurrentVersionId.HasValue && versions.Contains(d.CurrentVersionId.Value)).Select(d => d.DocumentId).ToArrayAsync(ct));
        return ids;
    }
}

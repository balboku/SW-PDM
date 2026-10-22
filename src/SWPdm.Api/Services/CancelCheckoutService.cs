namespace SWPdm.Api.Services;

using Microsoft.EntityFrameworkCore;
using SWPdm.Sample.Data;

// Single-document contract only. Batch restoration needs a separate, schema-backed workflow.
public sealed record UndoCheckoutRequest(string Actor, DateTimeOffset? ExpectedLockTime = null,
    long? ExpectedVersionId = null, bool IncludeBatch = false, Guid? BatchId = null);

public sealed class CancelCheckoutConflictException(string message) : Exception(message);

public sealed class CancelCheckoutService(PdmDbContext db)
{
    public async Task<object> PreviewAsync(long documentId, string actor, CancellationToken ct)
    {
        actor = NormalizeActor(actor);
        var document = await db.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.DocumentId == documentId, ct)
            ?? throw new KeyNotFoundException("找不到圖檔，請重新整理圖檔中心。");
        bool canRestore = document.CheckedOutAt.HasValue && document.CurrentVersionId.HasValue &&
            string.Equals(document.CheckedOutBy, actor, StringComparison.OrdinalIgnoreCase);
        string reason = string.IsNullOrWhiteSpace(document.CheckedOutBy) ? "這份圖檔沒有出庫鎖，不需要再取消。" :
            !string.Equals(document.CheckedOutBy, actor, StringComparison.OrdinalIgnoreCase) ? "只能取消自己持有的出庫鎖，請確認操作人或聯絡出庫持有人。" :
            !canRestore ? "鎖定時間或目前版本不完整，請重新整理後再確認；未解除鎖定。" :
            "確認後只解除這份圖檔的出庫鎖，其他關聯檔保持原狀。";
        return new { document.DocumentId, document.FileName, Actor = actor, document.CheckedOutBy,
            document.CheckedOutAt, document.CurrentVersionId, CanRestore = canRestore, Reason = reason,
            Scope = "SingleDocument" };
    }

    public async Task<object> UndoAsync(long documentId, UndoCheckoutRequest request, CancellationToken ct)
    {
        string actor = NormalizeActor(request.Actor);
        if (request.IncludeBatch || request.BatchId.HasValue)
            throw new ArgumentException("本版只支援單一圖檔，未解除任何批次鎖定。");
        if (!request.ExpectedLockTime.HasValue || request.ExpectedVersionId is null or <= 0)
            throw new ArgumentException("請先重新檢查出庫狀態，再確認鎖定時間與目前版本。");

        // One conditional UPDATE closes the read/check/write race. No schema or migration needed.
        string normalizedActor = actor.ToLowerInvariant();
        int changed = await db.Documents.Where(d => d.DocumentId == documentId && d.CheckedOutBy != null &&
            d.CheckedOutBy.ToLower() == normalizedActor && d.CheckedOutAt == request.ExpectedLockTime &&
            d.CurrentVersionId == request.ExpectedVersionId).ExecuteUpdateAsync(setters => setters
                .SetProperty(d => d.CheckedOutBy, (string?)null)
                .SetProperty(d => d.CheckedOutAt, (DateTimeOffset?)null), ct);
        if (changed != 1)
            throw new CancelCheckoutConflictException("出庫人、鎖定時間或版本已改變，未解除鎖定。請重新檢查後再決定。");
        return new { RestoredCount = 1, RestoredDocumentIds = new[] { documentId }, Scope = "SingleDocument" };
    }

    private static string NormalizeActor(string? actor) => string.IsNullOrWhiteSpace(actor) || actor.Trim().Length > 100
        ? throw new ArgumentException("請填寫操作人（最多 100 字），不要代填他人的出庫名稱。") : actor.Trim();
}

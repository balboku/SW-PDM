namespace SWPdm.Api.Services;

public sealed class PdmCheckoutConflictException : InvalidOperationException
{
    public PdmCheckoutConflictException(
        string fileName,
        string? checkedOutBy,
        DateTimeOffset? checkedOutAt,
        string? requestedBy,
        string? reason = null)
        : base(BuildMessage(fileName, checkedOutBy, checkedOutAt, requestedBy, reason))
    {
        FileName = fileName;
        CheckedOutBy = checkedOutBy;
        CheckedOutAt = checkedOutAt;
        RequestedBy = requestedBy;
    }

    public string FileName { get; }

    public string? CheckedOutBy { get; }

    public DateTimeOffset? CheckedOutAt { get; }

    public string? RequestedBy { get; }

    private static string BuildMessage(
        string fileName,
        string? checkedOutBy,
        DateTimeOffset? checkedOutAt,
        string? requestedBy,
        string? reason)
    {
        string lockTime = checkedOutAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "未知時間";
        string owner = string.IsNullOrWhiteSpace(checkedOutBy) ? "未知使用者" : checkedOutBy;
        string requester = string.IsNullOrWhiteSpace(requestedBy) ? "未指定使用者" : requestedBy;
        string suffix = string.IsNullOrWhiteSpace(reason) ? string.Empty : $" {reason}";

        return $"入庫衝突：檔案 '{fileName}' 已被 {owner} 於 {lockTime} 出庫，目前使用者 '{requester}' 無法入庫或覆蓋。{suffix}";
    }
}

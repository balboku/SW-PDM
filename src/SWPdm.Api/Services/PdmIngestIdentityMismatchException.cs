namespace SWPdm.Api.Services;

public sealed class PdmIngestIdentityMismatchException : InvalidOperationException
{
    public PdmIngestIdentityMismatchException(
        long targetDocumentId,
        string? expectedPartNumber,
        string expectedDocumentType,
        string? actualPartNumber,
        string actualDocumentType,
        bool? canCreateNewDocument = null,
        string? partNumberChangeBlockReason = null)
        : base(
            $"無法入庫到目前文件。目標文件 ID {targetDocumentId} 的品號／類型為 " +
            $"'{expectedPartNumber ?? "(未設定)"}'／'{expectedDocumentType}'，" +
            $"但上傳檔案解析結果為 '{actualPartNumber ?? "(未設定)"}'／'{actualDocumentType}'。" +
            "請重新選擇正確檔案；檔名可以不同，但品號與文件類型必須一致。")
    {
        TargetDocumentId = targetDocumentId;
        ExpectedPartNumber = expectedPartNumber;
        ExpectedDocumentType = expectedDocumentType;
        ActualPartNumber = actualPartNumber;
        ActualDocumentType = actualDocumentType;
        CanCreateNewDocument = canCreateNewDocument ??
            (!string.Equals(expectedPartNumber?.Trim(), actualPartNumber?.Trim(), StringComparison.OrdinalIgnoreCase) &&
             string.Equals(expectedDocumentType, actualDocumentType, StringComparison.OrdinalIgnoreCase));
        PartNumberChangeBlockReason = partNumberChangeBlockReason;
    }

    public long TargetDocumentId { get; }

    public string? ExpectedPartNumber { get; }

    public string ExpectedDocumentType { get; }

    public string? ActualPartNumber { get; }

    public string ActualDocumentType { get; }

    public bool CanCreateNewDocument { get; }

    public string? PartNumberChangeBlockReason { get; }
}

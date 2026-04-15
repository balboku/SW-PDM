namespace SWPdm.Sample.Data.Entities;

public sealed class PdmNumberingRule
{
    public long RuleId { get; set; }
    
    /// <summary>
    /// Type of document, e.g. "Part", "Assembly", "Drawing"
    /// </summary>
    public required string DocumentType { get; set; }
    
    /// <summary>
    /// The pattern of the numbering rule, e.g. "PRT-{YYMM}-{SEQ:4}"
    /// </summary>
    public required string Pattern { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; }
}

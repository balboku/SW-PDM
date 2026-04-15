namespace SWPdm.Sample.Data.Entities;

public sealed class PdmNumberSequence
{
    public long SequenceId { get; set; }
    
    /// <summary>
    /// The prefix for the sequence, typically generated after parsing the pattern (without the sequence number)
    /// </summary>
    public required string Prefix { get; set; }
    
    /// <summary>
    /// The current maximum allocated value for this prefix.
    /// </summary>
    public int CurrentValue { get; set; }
    
    public DateTimeOffset LastUpdatedAt { get; set; }
}

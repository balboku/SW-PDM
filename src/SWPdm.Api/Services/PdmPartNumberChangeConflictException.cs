namespace SWPdm.Api.Services;

public sealed class PdmPartNumberChangeConflictException : InvalidOperationException
{
    public PdmPartNumberChangeConflictException(string message)
        : base(message)
    {
    }
}

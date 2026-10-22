namespace SWPdm.Api.Services;

public sealed class PdmWorkflowException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

namespace SWPdm.Sample.Services;

public enum CadReferencePurpose { AssemblyComponent, DrawingModel, DesignDependency, UnclassifiedDependency }

/// <summary>Native evidence, not a resolved PDM version. Null flags mean unknown, not false.</summary>
public sealed record CadReference(
    string Path,
    CadReferencePurpose Purpose,
    bool? IsVirtual = null,
    bool? IsSuppressed = null,
    string ParentConfiguration = "",
    string ChildConfiguration = "",
    string OccurrenceName = "",
    string NativeStatus = "Unknown",
    string EvidenceSource = "LegacyAdapter")
{
    public bool IsBom => Purpose is CadReferencePurpose.AssemblyComponent or CadReferencePurpose.DrawingModel;
    public bool CanIngest => IsBom && IsVirtual != true && IsSuppressed != true;
}

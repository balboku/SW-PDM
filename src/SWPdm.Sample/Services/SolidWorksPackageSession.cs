namespace SWPdm.Sample.Services;

using System.Runtime.InteropServices;
#if SOLIDWORKS_DOCUMENT_MANAGER
using SolidWorks.Interop.swdocumentmgr;
#endif

public sealed record CadPackageInspection(SolidWorksDocumentKind Kind, IReadOnlyList<string> Configurations,
    IReadOnlyList<CadReference> References)
{
    public IReadOnlyDictionary<string, string> ReferenceAliases { get; init; } = new Dictionary<string, string>();
}

/// <summary>Separate from ingestion: this adapter can open only copies in its own package workspace.</summary>
public sealed class SolidWorksPackageSession : IDisposable
{
    private readonly string workspace;
#if SOLIDWORKS_DOCUMENT_MANAGER
    private readonly SwDMClassFactory factory;
    private readonly SwDMApplication4 application;
#endif
    public SolidWorksPackageSession(string key, string workspaceRoot)
    {
        workspace = Path.GetFullPath(workspaceRoot);
        string name = Path.GetFileName(workspace);
        if (Path.GetDirectoryName(workspace) != Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) ||
            !name.StartsWith("swpdm_package_", StringComparison.Ordinal) ||
            !Guid.TryParseExact(name["swpdm_package_".Length..], "N", out _))
            throw new ArgumentException("Package adapter requires an owned GUID workspace.");
#if SOLIDWORKS_DOCUMENT_MANAGER
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Document Manager license is not configured.");
        factory = new SwDMClassFactory();
        application = (SwDMApplication4)factory.GetApplication(key)
            ?? throw new InvalidOperationException("Document Manager could not be initialized.");
#else
        throw new NotSupportedException("Document Manager is unavailable.");
#endif
    }

    public CadPackageInspection Inspect(string copiedFile)
    {
        GuardCopy(copiedFile);
#if SOLIDWORKS_DOCUMENT_MANAGER
        return Open(copiedFile, false, document => Read(document, copiedFile));
#else
        throw new NotSupportedException("Document Manager is unavailable.");
#endif
    }

    public void Rewrite(string copiedFile, IReadOnlyDictionary<string, string> replacements)
    {
        GuardCopy(copiedFile);
        foreach (string target in replacements.Values) GuardCopy(target);
#if SOLIDWORKS_DOCUMENT_MANAGER
        Open(copiedFile, true, document =>
        {
            // Read all configurations/feature references, then initialize the document-wide replacement cache.
            var evidence = Read(document, copiedFile);
            SwDMSearchOption? search = null;
            try
            {
                search = Search(copiedFile);
                string[] originals = Strings(document.GetAllExternalReferences4(search, out _, out _, out _));
                var changes = Changes(document);
                var cachedComponents = new List<KeyValuePair<string, string>>();
                foreach (var replacement in replacements)
                {
                    var matches = originals.Where(p => Effective(p, changes, copiedFile).Equals(replacement.Key, StringComparison.OrdinalIgnoreCase)).ToArray();
                    if (matches.Length == 0) { cachedComponents.Add(replacement); continue; }
                    foreach (string original in matches) document.ReplaceReference(original, replacement.Value);
                }
                // Some files carry component-specific replacements whose original PathName remains cached.
                // Use the exact native occurrence/configuration evidence, never a filename match.
                foreach (var replacement in cachedComponents)
                    RewriteCachedComponents(document, evidence, replacement.Key, replacement.Value);
                if (document.Save() != SwDmDocumentSaveError.swDmDocumentSaveErrorNone)
                    throw new InvalidOperationException("Document Manager could not save the package copy.");
                return true;
            }
            finally { Release(search); }
        });
#else
        throw new NotSupportedException("Document Manager is unavailable.");
#endif
    }

    private void GuardCopy(string path)
    {
        string full = Path.GetFullPath(path);
        if (!full.StartsWith(workspace + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
            throw new InvalidOperationException("Only an existing package copy may be opened.");
        for (string? item = full; item != null && item.Length >= workspace.Length; item = Path.GetDirectoryName(item))
            if ((File.GetAttributes(item) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Package copies cannot use reparse points.");
    }

#if SOLIDWORKS_DOCUMENT_MANAGER
    private T Open<T>(string path, bool writable, Func<SwDMDocument18, T> action)
    {
        SwDMDocument18? document = null;
        try
        {
            document = (SwDMDocument18)application.GetDocument(path, Kind(path), !writable, out var error);
            if (document == null || error != SwDmDocumentOpenError.swDmDocumentOpenErrorNone)
                throw new InvalidOperationException("Document Manager could not open the package copy.");
            return action(document);
        }
        finally { document?.CloseDoc(); Release(document); }
    }

    private CadPackageInspection Read(SwDMDocument18 document, string path)
    {
        var result = new List<CadReference>();
        var changes = Changes(document);
        var referenceAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string Observe(string original)
        {
            if (referenceAliases.TryGetValue(original, out string? known)) return known;
            string effective = Effective(original, changes, path);
            referenceAliases[original] = effective;
            return effective;
        }
        foreach (var changed in changes) { Observe(changed.Key); Observe(changed.Value); }
        SwDMSearchOption? search = null; SwDMConfigurationMgr? manager = null;
        try
        {
            search = Search(path);
            string[] saved = Strings(document.GetAllExternalReferences4(search, out _, out object virtuals, out _));
            bool drawing = Kind(path) == SwDmDocumentType.swDmDocumentDrawing;
            string[] configurations = [];
            if (!drawing)
            {
                manager = document.ConfigurationManager;
                configurations = Strings(manager.GetConfigurationNames());
                if (configurations.Length == 0) throw new InvalidOperationException("No native configuration evidence.");
                foreach (string configuration in configurations)
                {
                    SwDMExternalReferenceOption2? feature = null; SwDMConfiguration? config = null;
                    try
                    {
                        feature = application.GetExternalReferenceOptionObject2();
                        feature.Configuration = configuration; feature.NeedSuppress = true; feature.SearchOption = search;
                        _ = document.GetExternalFeatureReferences2(feature);
                        if (Kind(path) == SwDmDocumentType.swDmDocumentAssembly)
                        {
                            config = manager.GetConfigurationByName(configuration);
                            ((ISwDMConfiguration11)config).GetReplacedComponents(out object replacedNames, out object replacedConfigurations,
                                out object replacedPaths, out _);
                            string[] replaced = Strings(replacedNames), replacementPaths = Strings(replacedPaths);
                            string[] replacementConfigurations = Strings(replacedConfigurations);
                            if (replaced.Length != replacementPaths.Length || replaced.Length != replacementConfigurations.Length)
                                throw new InvalidOperationException("Native replaced-component evidence differs in length.");
                            var components = ((ISwDMConfiguration2)config).GetComponents() as Array;
                            if (components != null) foreach (object item in components)
                            {
                                try
                                {
                                    var component = (ISwDMComponent6)item;
                                    string occurrenceKey = ComponentKey(component);
                                    int replacement = Array.FindIndex(replaced, name => name.Equals(occurrenceKey, StringComparison.OrdinalIgnoreCase));
                                    string childConfiguration = component.ConfigurationName;
                                    if (replacement >= 0)
                                    {
                                        referenceAliases[component.PathName] = Effective(replacementPaths[replacement], changes, path);
                                        childConfiguration = replacementConfigurations[replacement];
                                    }
                                    result.Add(new(Observe(component.PathName), CadReferencePurpose.AssemblyComponent,
                                        component.IsVirtual, component.IsSuppressed(), configuration, childConfiguration,
                                        occurrenceKey, EvidenceSource: "Package:GetComponents+GetChangedReferences+GetReplacedComponents"));
                                }
                                finally { Release(item); }
                            }
                        }
                        foreach (string reference in Strings(feature.ExternalReferences))
                        {
                            string effective = Observe(reference);
                            if (!result.Any(r => r.Path.Equals(effective, StringComparison.OrdinalIgnoreCase) && r.ParentConfiguration == configuration))
                                result.Add(new(effective, CadReferencePurpose.DesignDependency, ParentConfiguration: configuration,
                                    EvidenceSource: "Package:GetExternalFeatureReferences2"));
                        }
                    }
                    finally { Release(config); Release(feature); }
                }
            }
            for (int i = 0; i < saved.Length; i++)
            {
                string effective = Observe(saved[i]);
                if (result.Any(r => r.Path.Equals(effective, StringComparison.OrdinalIgnoreCase))) continue;
                bool? isVirtual = virtuals is Array values && i < values.Length
                    ? Convert.ToBoolean(values.GetValue(i + values.GetLowerBound(0))) : null;
                result.Add(new(effective, drawing ? CadReferencePurpose.DrawingModel : CadReferencePurpose.UnclassifiedDependency,
                    isVirtual, EvidenceSource: "Package:GetAllExternalReferences4"));
            }
            return new(drawing ? SolidWorksDocumentKind.Drawing : Kind(path) == SwDmDocumentType.swDmDocumentAssembly
                ? SolidWorksDocumentKind.Assembly : SolidWorksDocumentKind.Part, configurations, result) { ReferenceAliases = referenceAliases };
        }
        finally { Release(manager); Release(search); }
    }

    private SwDMSearchOption Search(string path)
    {
        var search = application.GetSearchOptionObject();
        search.SearchFilters = (int)(SwDmSearchFilters.SwDmSearchExternalReference | SwDmSearchFilters.SwDmSearchForPart |
            SwDmSearchFilters.SwDmSearchForAssembly | SwDmSearchFilters.SwDmSearchInContextReference | SwDmSearchFilters.SwDmSearchRootAssemblyFolder);
        search.AddSearchPath(Path.GetDirectoryName(path)!);
        return search;
    }
    private static void RewriteCachedComponents(SwDMDocument18 document, CadPackageInspection evidence, string original, string destination)
    {
        var occurrences = evidence.References.Where(r => r.Purpose == CadReferencePurpose.AssemblyComponent &&
            r.Path.Equals(original, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (occurrences.Length == 0) throw new InvalidOperationException("Reference is not in a native replacement cache or component occurrence.");
        SwDMConfigurationMgr? manager = null;
        try
        {
            manager = document.ConfigurationManager;
            foreach (var occurrence in occurrences)
            {
                SwDMConfiguration? config = null;
                try
                {
                    config = manager.GetConfigurationByName(occurrence.ParentConfiguration);
                    var components = ((ISwDMConfiguration2)config).GetComponents() as Array;
                    int replaced = 0;
                    if (components != null) foreach (object item in components)
                    {
                        try
                        {
                            var component = (ISwDMComponent6)item;
                            if (!ComponentKey(component).Equals(occurrence.OccurrenceName, StringComparison.Ordinal) || component.IsVirtual) continue;
                            // Name+configuration was read from this very native document before any write.
                            if (!component.Replace(destination, occurrence.ChildConfiguration, true))
                                throw new InvalidOperationException("Native component reference replacement failed.");
                            replaced++;
                        }
                        finally { Release(item); }
                    }
                    if (replaced != 1) throw new InvalidOperationException("Native component occurrence is ambiguous or missing.");
                }
                finally { Release(config); }
            }
        }
        finally { Release(manager); }
    }
    private static Dictionary<string, string> Changes(SwDMDocument18 document)
    {
        document.GetChangedReferences(out object oldPaths, out object newPaths);
        var old = Strings(oldPaths); var updated = Strings(newPaths);
        if (old.Length != updated.Length) throw new InvalidOperationException("Native changed-reference arrays differ.");
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < old.Length; i++) result[old[i]] = updated[i];
        return result;
    }
    private static string ComponentKey(ISwDMComponent6 component) => component switch
    {
        ISwDMComponent11 modern => modern.SelectName2,
        ISwDMComponent7 legacy => legacy.SelectName,
        _ => throw new NotSupportedException("No native qualified occurrence identity.")
    };
    private static string Effective(string path, IReadOnlyDictionary<string, string> changes, string parent)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (changes.TryGetValue(path, out string? next))
        {
            if (path.Equals(next, StringComparison.OrdinalIgnoreCase)) break; // Native cache can contain identity entries.
            if (!visited.Add(path)) throw new InvalidOperationException("Native reference replacement cycle.");
            path = next;
        }
        if (File.Exists(path)) return Path.GetFullPath(path);
        // Changed-reference paths can refer to the old package staging directory after relocation.
        string local = Path.Combine(Path.GetDirectoryName(parent)!, Path.GetFileName(path));
        return File.Exists(local) ? Path.GetFullPath(local) : path;
    }
    private static string[] Strings(object? value)
    {
        if (value == null) return [];
        if (value is not Array array) throw new InvalidOperationException("Native reference evidence is not an array.");
        var result = array.Cast<object?>().Select(x => x?.ToString() ?? "").ToArray();
        if (result.Any(string.IsNullOrWhiteSpace)) throw new InvalidOperationException("Native reference evidence contains empty entries.");
        return result;
    }
    private static SwDmDocumentType Kind(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".sldasm" => SwDmDocumentType.swDmDocumentAssembly, ".slddrw" => SwDmDocumentType.swDmDocumentDrawing,
        ".sldprt" => SwDmDocumentType.swDmDocumentPart, _ => throw new NotSupportedException("Unsupported package file type.")
    };
    private static void Release(object? value) { if (OperatingSystem.IsWindows() && value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
#endif
    public void Dispose()
    {
#if SOLIDWORKS_DOCUMENT_MANAGER
        Release(application); Release(factory);
#endif
    }
}

namespace SWPdm.Sample.Services;

using System.Collections.Generic;
using System.IO;
using System.Linq;
#if SOLIDWORKS_DOCUMENT_MANAGER
using System.Runtime.InteropServices;
using SolidWorks.Interop.swdocumentmgr;
#endif

/// <summary>
/// Reads SolidWorks metadata and external references through the
/// SolidWorks Document Manager API without launching SolidWorks.
/// </summary>
#if SOLIDWORKS_DOCUMENT_MANAGER
public sealed class SolidWorksDocumentManagerService : IDisposable
{
    private readonly SwDMClassFactory _classFactory;
    private readonly SwDMApplication4 _documentManager;
    private bool _disposed;

    public SolidWorksDocumentManagerService(string licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            throw new ArgumentException("Document Manager license key is required.", nameof(licenseKey));
        }

        _classFactory = new SwDMClassFactory();
        _documentManager = (SwDMApplication4)_classFactory.GetApplication(licenseKey)
            ?? throw new InvalidOperationException(
                "Unable to initialize SolidWorks Document Manager. Verify the license key and DLL registration.");
    }

    /// <summary>
    /// Opens a SolidWorks document in read-only mode and returns:
    /// 1. document-level custom properties
    /// 2. configuration-level custom properties
    /// 3. external references if the file is an assembly
    /// </summary>
    public SolidWorksParseResult Parse(
        string filePath,
        IEnumerable<string>? additionalSearchPaths = null)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path is required.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("SolidWorks file was not found.", filePath);
        }

        SwDmDocumentType documentType = ResolveInteropDocumentType(filePath);
        SwDMDocument18? document = null;

        try
        {
            document = (SwDMDocument18)_documentManager.GetDocument(
                filePath,
                documentType,
                true,
                out SwDmDocumentOpenError openError);

            if (document is null || openError != SwDmDocumentOpenError.swDmDocumentOpenErrorNone)
            {
                throw new InvalidOperationException(
                    $"Unable to open SolidWorks document '{filePath}'. OpenError={openError}");
            }

            IReadOnlyDictionary<string, SolidWorksCustomProperty> documentProperties =
                ReadDocumentCustomProperties(document);

            IReadOnlyDictionary<string, IReadOnlyDictionary<string, SolidWorksCustomProperty>> configurationProperties =
                ReadConfigurationCustomProperties(document);

            IReadOnlyList<string> referencedFiles =
                documentType == SwDmDocumentType.swDmDocumentAssembly
                    ? ReadAssemblyReferences(document, filePath, additionalSearchPaths)
                    : Array.Empty<string>();

            return new SolidWorksParseResult(
                filePath,
                MapDocumentKind(documentType),
                documentProperties,
                configurationProperties,
                referencedFiles);
        }
        finally
        {
            CloseAndRelease(document);
        }
    }

    /// <summary>
    /// Writes a top-level document custom property to the file.
    /// Opens the file with readOnly = false.
    /// </summary>
    public void WriteCustomProperty(string filePath, string propertyName, string propertyValue)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path is required.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("SolidWorks file was not found.", filePath);
        }

        SwDmDocumentType documentType = ResolveInteropDocumentType(filePath);
        SwDMDocument18? document = null;

        try
        {
            document = (SwDMDocument18)_documentManager.GetDocument(
                filePath,
                documentType,
                false, // false = not read only -> writable
                out SwDmDocumentOpenError openError);

            if (document is null || (openError != SwDmDocumentOpenError.swDmDocumentOpenErrorNone && openError != SwDmDocumentOpenError.swDmDocumentOpenErrorFileReadOnly))
            {
                throw new InvalidOperationException(
                    $"Unable to open SolidWorks document '{filePath}' for writing. OpenError={openError}");
            }

            // swDmCustomInfoText = 30
            document.AddCustomProperty(propertyName, (SwDmCustomInfoType)30, propertyValue);
            document.SetCustomProperty(propertyName, propertyValue);
            _ = document.Save();
        }
        finally
        {
            CloseAndRelease(document);
        }
    }

    private IReadOnlyDictionary<string, SolidWorksCustomProperty> ReadDocumentCustomProperties(
        SwDMDocument18 document)
    {
        Dictionary<string, SolidWorksCustomProperty> result =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (string propertyName in ConvertToStringArray(document.GetCustomPropertyNames()))
        {
            string value = document.GetCustomProperty2(propertyName, out SwDmCustomInfoType propertyType) ?? string.Empty;
            result[propertyName] = new SolidWorksCustomProperty(propertyName, value, propertyType.ToString());
        }

        return result;
    }

    private IReadOnlyDictionary<string, IReadOnlyDictionary<string, SolidWorksCustomProperty>>
        ReadConfigurationCustomProperties(SwDMDocument18 document)
    {
        Dictionary<string, IReadOnlyDictionary<string, SolidWorksCustomProperty>> result =
            new(StringComparer.OrdinalIgnoreCase);

        SwDMConfigurationMgr? configurationManager = null;

        try
        {
            configurationManager = document.ConfigurationManager;

            if (configurationManager is null)
            {
                return result;
            }

            foreach (string configurationName in ConvertToStringArray(configurationManager.GetConfigurationNames()))
            {
                SwDMConfiguration? configuration = null;

                try
                {
                    configuration = configurationManager.GetConfigurationByName(configurationName);

                    if (configuration is null)
                    {
                        continue;
                    }

                    Dictionary<string, SolidWorksCustomProperty> configurationProperties =
                        new(StringComparer.OrdinalIgnoreCase);

                    foreach (string propertyName in ConvertToStringArray(configuration.GetCustomPropertyNames()))
                    {
                        if (configuration is not ISwDMConfiguration14 configuration14)
                        {
                            continue;
                        }

                        string value = configuration14.GetCustomProperty2(
                            propertyName,
                            out SwDmCustomInfoType propertyType) ?? string.Empty;

                        configurationProperties[propertyName] =
                            new SolidWorksCustomProperty(propertyName, value, propertyType.ToString());
                    }

                    result[configurationName] = configurationProperties;
                }
                finally
                {
                    ReleaseComObject(configuration);
                }
            }

            return result;
        }
        finally
        {
            ReleaseComObject(configurationManager);
        }
    }

    private IReadOnlyList<string> ReadAssemblyReferences(
        SwDMDocument18 document,
        string filePath,
        IEnumerable<string>? additionalSearchPaths)
    {
        SwDMConfigurationMgr? configurationManager = null;
        SwDMSearchOption? searchOption = null;
        SwDMExternalReferenceOption2? externalReferenceOption = null;

        try
        {
            configurationManager = document.ConfigurationManager;
            string activeConfigurationName =
                configurationManager?.GetActiveConfigurationName() ?? string.Empty;

            searchOption = _documentManager.GetSearchOptionObject();
            searchOption.SearchFilters =
                (int)(
                    SwDmSearchFilters.SwDmSearchExternalReference |
                    SwDmSearchFilters.SwDmSearchForPart |
                    SwDmSearchFilters.SwDmSearchForAssembly |
                    SwDmSearchFilters.SwDmSearchInContextReference);

            string? parentFolder = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(parentFolder))
            {
                searchOption.AddSearchPath(parentFolder);
            }

            if (additionalSearchPaths is not null)
            {
                foreach (string searchPath in additionalSearchPaths.Where(p => !string.IsNullOrWhiteSpace(p)))
                {
                    searchOption.AddSearchPath(searchPath);
                }
            }

            externalReferenceOption = _documentManager.GetExternalReferenceOptionObject2();
            externalReferenceOption.Configuration = activeConfigurationName;
            externalReferenceOption.NeedSuppress = true;
            externalReferenceOption.SearchOption = searchOption;

            _ = document.GetExternalFeatureReferences2(externalReferenceOption);

            return ConvertToStringArray(externalReferenceOption.ExternalReferences)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToArray();
        }
        finally
        {
            ReleaseComObject(configurationManager);
            ReleaseComObject(searchOption);
            ReleaseComObject(externalReferenceOption);
        }
    }

    private static SwDmDocumentType ResolveInteropDocumentType(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".sldprt" => SwDmDocumentType.swDmDocumentPart,
            ".sldasm" => SwDmDocumentType.swDmDocumentAssembly,
            ".slddrw" => SwDmDocumentType.swDmDocumentDrawing,
            _ => throw new NotSupportedException(
                $"Unsupported SolidWorks file type: '{Path.GetExtension(filePath)}'.")
        };
    }

    private static SolidWorksDocumentKind MapDocumentKind(SwDmDocumentType documentType)
    {
        return documentType switch
        {
            SwDmDocumentType.swDmDocumentPart => SolidWorksDocumentKind.Part,
            SwDmDocumentType.swDmDocumentAssembly => SolidWorksDocumentKind.Assembly,
            SwDmDocumentType.swDmDocumentDrawing => SolidWorksDocumentKind.Drawing,
            _ => throw new NotSupportedException($"Unsupported SolidWorks document type: {documentType}.")
        };
    }

    private static string[] ConvertToStringArray(object? comArray)
    {
        if (comArray is null)
        {
            return Array.Empty<string>();
        }

        if (comArray is string[] typedArray)
        {
            return typedArray;
        }

        if (comArray is object[] objectArray)
        {
            return objectArray
                .Where(item => item is not null)
                .Select(item => item!.ToString() ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
        }

        return Array.Empty<string>();
    }

    private static void CloseAndRelease(SwDMDocument18? document)
    {
        if (document is null)
        {
            return;
        }

        try
        {
            document.CloseDoc();
        }
        finally
        {
            ReleaseComObject(document);
        }
    }

    private static void ReleaseComObject(object? comObject)
    {
#pragma warning disable CA1416 // Validate platform compatibility
        if (comObject is not null && Marshal.IsComObject(comObject))
        {
            Marshal.FinalReleaseComObject(comObject);
        }
#pragma warning restore CA1416 // Validate platform compatibility
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SolidWorksDocumentManagerService));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ReleaseComObject(_documentManager);
        ReleaseComObject(_classFactory);
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
#else
public sealed class SolidWorksDocumentManagerService : IDisposable
{
    public SolidWorksDocumentManagerService(string licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            throw new ArgumentException("Document Manager license key is required.", nameof(licenseKey));
        }
    }

    public SolidWorksParseResult Parse(
        string filePath,
        IEnumerable<string>? additionalSearchPaths = null)
    {
        // 模擬解析結果，為了讓使用者在缺少 SolidWorks DLL 的情況下也能成功測試入庫流程
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var docType = ext == ".sldasm" ? SolidWorksDocumentKind.Assembly :
                      ext == ".slddrw" ? SolidWorksDocumentKind.Drawing :
                      SolidWorksDocumentKind.Part;

        string fakePartNumber = "TEST-" + Guid.NewGuid().ToString("N")[..6].ToUpper();

        var mockProperties = new Dictionary<string, SolidWorksCustomProperty>(StringComparer.OrdinalIgnoreCase)
        {
            { "PartNumber", new SolidWorksCustomProperty("PartNumber", fakePartNumber, "Text") },
            { "Material", new SolidWorksCustomProperty("Material", "Mock Material (No DLL)", "Text") },
            { "Revision", new SolidWorksCustomProperty("Revision", "1.0", "Text") },
            { "Designer", new SolidWorksCustomProperty("Designer", "System Mock", "Text") }
        };

        return new SolidWorksParseResult(
            filePath,
            docType,
            mockProperties,
            new Dictionary<string, IReadOnlyDictionary<string, SolidWorksCustomProperty>>(),
            Array.Empty<string>()
        );
    }

    public void Dispose()
    {
    }

    public void WriteCustomProperty(string filePath, string propertyName, string propertyValue)
    {
        // No-Op since the SolidWorks Document Manager interop is not referenced
    }
}
#endif

public enum SolidWorksDocumentKind
{
    Part = 1,
    Assembly = 2,
    Drawing = 3
}

public sealed record SolidWorksParseResult(
    string FilePath,
    SolidWorksDocumentKind DocumentType,
    IReadOnlyDictionary<string, SolidWorksCustomProperty> DocumentProperties,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, SolidWorksCustomProperty>> ConfigurationProperties,
    IReadOnlyList<string> ReferencedFilePaths);

public sealed record SolidWorksCustomProperty(
    string Name,
    string Value,
    string PropertyType);

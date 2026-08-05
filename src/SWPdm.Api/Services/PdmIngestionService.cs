namespace SWPdm.Api.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SWPdm.Api.Configuration;
using SWPdm.Api.Contracts;
using SWPdm.Sample.Data;
using SWPdm.Sample.Data.Entities;
using SWPdm.Sample.Services;
using System.Security.Cryptography;

public sealed class PdmIngestionService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".sldprt",
        ".sldasm",
        ".slddrw"
    };

    private readonly PdmDbContext _dbContext;
    private readonly IOptions<LocalStorageOptions> _localStorageOptions;
    private readonly IOptions<SolidWorksDocumentManagerOptions> _solidWorksOptions;
    private readonly LocalStorageService _localStorageService;
    private readonly SolidWorksDocumentManagerServiceFactory _solidWorksDocumentManagerServiceFactory;
    private readonly ILogger<PdmIngestionService> _logger;

    public PdmIngestionService(
        PdmDbContext dbContext,
        IOptions<LocalStorageOptions> localStorageOptions,
        IOptions<SolidWorksDocumentManagerOptions> solidWorksOptions,
        LocalStorageService localStorageService,
        SolidWorksDocumentManagerServiceFactory solidWorksDocumentManagerServiceFactory,
        ILogger<PdmIngestionService> logger)
    {
        _dbContext = dbContext;
        _localStorageOptions = localStorageOptions;
        _solidWorksOptions = solidWorksOptions;
        _localStorageService = localStorageService;
        _solidWorksDocumentManagerServiceFactory = solidWorksDocumentManagerServiceFactory;
        _logger = logger;
    }

    public async Task<IngestCadFileResponse> IngestAsync(
        IngestCadFileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.LocalFilePath))
        {
            throw new ArgumentException("LocalFilePath is required.", nameof(request));
        }

        string rootFilePath = Path.GetFullPath(request.LocalFilePath);
        if (!File.Exists(rootFilePath))
        {
            throw new FileNotFoundException("The SolidWorks file to ingest was not found.", rootFilePath);
        }

        EnsureSupportedExtension(rootFilePath);

        string[] baseSearchPaths = _solidWorksOptions.Value.ReferenceSearchPaths
            .Concat(request.AdditionalSearchPaths ?? Array.Empty<string>())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        using SolidWorksDocumentManagerService documentManager = _solidWorksDocumentManagerServiceFactory.Create();

        List<string> issues = new();
        Dictionary<string, IngestedCadNode> cache = new(StringComparer.OrdinalIgnoreCase);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        IngestedCadNode root = await IngestFileInternalAsync(
            rootFilePath,
            request.IngestReferencedFiles,
            baseSearchPaths,
            documentManager,
            cache,
            issues,
            true, // isRoot
            request.TargetDocumentId,
            request.CreateNewDocumentForPartNumberChange,
            request.UploadedBy,
            request.ChangeReason,
            cancellationToken);

        PartNumberChangeResponse? partNumberChange = null;
        if (request.CreateNewDocumentForPartNumberChange)
        {
            PdmDocumentIdentityChange identityChange = await _dbContext.DocumentIdentityChanges
                .AsNoTracking()
                .SingleAsync(x => x.TargetDocumentId == root.DocumentId, cancellationToken);
            partNumberChange = new PartNumberChangeResponse(
                identityChange.IdentityChangeId,
                identityChange.SourceDocumentId,
                identityChange.SourceVersionId,
                identityChange.TargetDocumentId,
                identityChange.OldPartNumber,
                identityChange.NewPartNumber,
                identityChange.ChangeReason,
                identityChange.ChangedBy,
                identityChange.CreatedAt);
        }

        await transaction.CommitAsync(cancellationToken);

        IReadOnlyList<IngestedFileResponse> files = cache.Values
            .OrderBy(x => x.SourceFilePath, StringComparer.OrdinalIgnoreCase)
            .Select(x => new IngestedFileResponse(
                SourceFilePath: x.SourceFilePath,
                DocumentId: x.DocumentId,
                VersionId: x.VersionId,
                DocumentType: x.DocumentType,
                PartNumber: x.PartNumber,
                StorageFileId: x.StorageFileId,
                CreatedDocument: x.CreatedDocument,
                VersionNo: x.VersionNo))
            .ToArray();

        // 品號分支不得回寫既有歷史 BOM；一般入庫才沿用既有 missing-link 修復行為。
        if (!request.CreateNewDocumentForPartNumberChange)
        {
            await HealMissingLinksAsync(cancellationToken);
        }

        return new IngestCadFileResponse(
            RootDocumentId: root.DocumentId,
            RootVersionId: root.VersionId,
            RootDocumentType: root.DocumentType,
            RootStorageFileId: root.StorageFileId,
            ProcessedFileCount: files.Count,
            Files: files,
            Issues: issues,
            PartNumberChange: partNumberChange);
    }

    private async Task HealMissingLinksAsync(CancellationToken cancellationToken)
    {
        var missingLinks = await _dbContext.BomOccurrences
            .Where(x => x.ChildVersionId == null)
            .ToListAsync(cancellationToken);

        if (missingLinks.Count == 0) return;

        bool changed = false;
        foreach (var link in missingLinks)
        {
            string fileName = Path.GetFileName(link.SourceReferencePath).ToLower();
            var resolved = await _dbContext.DocumentVersions
                .Where(v => v.OriginalFileName.ToLower() == fileName)
                .OrderByDescending(v => v.VersionNo)
                .FirstOrDefaultAsync(cancellationToken);

            if (resolved != null)
            {
                link.ChildVersionId = resolved.VersionId;
                link.ReferenceStatus = "Resolved";
                changed = true;
            }
        }

        if (changed)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<IngestedCadNode> IngestFileInternalAsync(
        string filePath,
        bool ingestReferencedFiles,
        IReadOnlyCollection<string> inheritedSearchPaths,
        SolidWorksDocumentManagerService documentManager,
        IDictionary<string, IngestedCadNode> cache,
        ICollection<string> issues,
        bool isRoot,
        long? targetDocumentId,
        bool createNewDocumentForPartNumberChange,
        string? uploadedBy,
        string? changeReason,
        CancellationToken cancellationToken)
    {
        string normalizedPath = Path.GetFullPath(filePath);

        if (cache.TryGetValue(normalizedPath, out IngestedCadNode? cached))
        {
            return cached;
        }

        List<string> effectiveSearchPaths = inheritedSearchPaths.ToList();
        string? folder = Path.GetDirectoryName(normalizedPath);
        if (!string.IsNullOrWhiteSpace(folder) && !effectiveSearchPaths.Contains(folder, StringComparer.OrdinalIgnoreCase))
        {
            effectiveSearchPaths.Add(folder);
        }

        SolidWorksParseResult parseResult = documentManager.Parse(normalizedPath, effectiveSearchPaths);
        string documentType = MapDocumentType(parseResult.DocumentType);
        string? partNumber = ExtractProperty(parseResult, "PartNumber", "Number", "Part No", "PartNo", "品號");

        // 強制驗證：PartNumber / 品號 必須存在於 CAD 檔案的自訂屬性中
        if (string.IsNullOrWhiteSpace(partNumber))
        {
            throw new InvalidOperationException(
                $"解析失敗：CAD 檔案內部未設定 PartNumber 或 品號 屬性，請在 SolidWorks 填寫後再上傳。(檔案：{Path.GetFileName(normalizedPath)})");
        }

        partNumber = partNumber.Trim();

        PdmDocument? existingDocument;
        PdmDocument? identityChangeSource = null;
        long? identityChangeSourceVersionId = null;
        if (isRoot && targetDocumentId.HasValue)
        {
            PdmDocument? targetDocument = await _dbContext.Documents
                .SingleOrDefaultAsync(
                    x => x.DocumentId == targetDocumentId.Value,
                    cancellationToken);

            if (targetDocument is null)
            {
                throw new ArgumentException(
                    $"找不到目標文件 ID {targetDocumentId.Value}，請重新整理圖檔中心後再試。",
                    nameof(targetDocumentId));
            }

            bool partNumberMatches = string.Equals(
                targetDocument.PartNumber?.Trim(),
                partNumber,
                StringComparison.OrdinalIgnoreCase);
            bool documentTypeMatches = string.Equals(
                targetDocument.DocumentType,
                documentType,
                StringComparison.OrdinalIgnoreCase);

            if (!createNewDocumentForPartNumberChange)
            {
                if (!partNumberMatches || !documentTypeMatches)
                {
                    bool canCreateNewDocument = documentTypeMatches && !partNumberMatches;
                    string? blockReason = null;

                    if (canCreateNewDocument && string.IsNullOrWhiteSpace(targetDocument.PartNumber))
                    {
                        canCreateNewDocument = false;
                        blockReason = "原文件沒有有效品號，不能建立可追溯的新品號文件。";
                    }
                    else if (canCreateNewDocument && await PartNumberExistsAsync(documentType, partNumber, cancellationToken))
                    {
                        canCreateNewDocument = false;
                        blockReason = $"新品號 {partNumber} 已被同類型文件使用，不能建立重複文件。";
                    }
                    else if (canCreateNewDocument && !targetDocument.CurrentVersionId.HasValue)
                    {
                        canCreateNewDocument = false;
                        blockReason = "原文件沒有目前版本，不能建立可追溯的新品號文件。";
                    }
                    else if (canCreateNewDocument &&
                             (string.IsNullOrWhiteSpace(targetDocument.CheckedOutBy) ||
                              string.IsNullOrWhiteSpace(uploadedBy) ||
                              !string.Equals(targetDocument.CheckedOutBy, uploadedBy, StringComparison.OrdinalIgnoreCase)))
                    {
                        canCreateNewDocument = false;
                        blockReason = "只有原文件的出庫持有人可以建立新品號文件。";
                    }
                    else if (canCreateNewDocument && targetDocument.CheckedOutAt is null)
                    {
                        canCreateNewDocument = false;
                        blockReason = "原文件的出庫鎖缺少有效時間，請復原後重新出庫。";
                    }

                    throw new PdmIngestIdentityMismatchException(
                        targetDocument.DocumentId,
                        targetDocument.PartNumber,
                        targetDocument.DocumentType,
                        partNumber,
                        documentType,
                        canCreateNewDocument,
                        blockReason);
                }

                existingDocument = targetDocument;
            }
            else
            {
                if (!documentTypeMatches)
                {
                    throw new PdmIngestIdentityMismatchException(
                        targetDocument.DocumentId,
                        targetDocument.PartNumber,
                        targetDocument.DocumentType,
                        partNumber,
                        documentType,
                        false,
                        "文件類型不同，不能使用品號分支。");
                }

                if (partNumberMatches)
                {
                    throw new PdmPartNumberChangeConflictException(
                        "目前檔案品號與原文件相同，請使用一般 Check-in 建立新版次。");
                }

                if (string.IsNullOrWhiteSpace(targetDocument.PartNumber))
                {
                    throw new PdmPartNumberChangeConflictException(
                        "原文件沒有有效品號，不能使用新品號分支；請先走受控的品號更正流程。");
                }

                if (string.IsNullOrWhiteSpace(changeReason))
                {
                    throw new ArgumentException(
                        "另存為新料號時必須填寫變更原因。",
                        nameof(changeReason));
                }

                EnsureCheckoutLockAllowsIngest(
                    targetDocument,
                    Path.GetFileName(normalizedPath),
                    uploadedBy);

                if (!targetDocument.CurrentVersionId.HasValue)
                {
                    throw new PdmPartNumberChangeConflictException(
                        "原文件沒有目前版本，無法建立可追溯的新品號文件。");
                }

                if (await PartNumberExistsAsync(documentType, partNumber, cancellationToken))
                {
                    throw new PdmPartNumberChangeConflictException(
                        $"新品號 {partNumber} 已被同類型文件使用，請重新確認 CAD 品號。");
                }

                identityChangeSource = targetDocument;
                identityChangeSourceVersionId = targetDocument.CurrentVersionId.Value;
                existingDocument = null;
            }
        }
        else
        {
            if (createNewDocumentForPartNumberChange)
            {
                throw new ArgumentException(
                    "另存為新料號必須指定來源文件 ID。",
                    nameof(targetDocumentId));
            }

            // 一般入庫仍以 CAD 內部品號與類型辨識文件；檔名只作為版本中繼資料。
            existingDocument = await FindDocumentForIngestAsync(
                documentType,
                partNumber,
                normalizedPath,
                cancellationToken);
        }
        
        string? revision = ExtractProperty(parseResult, "Revision", "Rev", "版次");

        // 業務邏輯：同料號且同版次則拒絕存檔 (優先於出庫鎖定檢查)
        if (existingDocument != null && !string.IsNullOrWhiteSpace(revision))
        {
            bool isSameRevision = string.Equals(
                revision.Trim(), 
                existingDocument.RevisionLabel?.Trim(), 
                StringComparison.OrdinalIgnoreCase);

            if (isSameRevision)
            {
                throw new InvalidOperationException(
                    $"入庫失敗：上傳的檔案版次 ({revision}) 與系統現有版次相同。若要更新圖檔內容，請先於 SolidWorks 中變更版次屬性後再上傳。");
            }
        }

        if (existingDocument != null)
        {
            _logger.LogWarning("Found existing document ID {DocumentId} for partNumber {PartNumber}. Checking checkout lock...", existingDocument.DocumentId, partNumber);
            EnsureCheckoutLockAllowsIngest(existingDocument, Path.GetFileName(normalizedPath), uploadedBy);
        }
        else
        {
            _logger.LogWarning("No existing document found for partNumber {PartNumber} and type {DocumentType}.", partNumber, documentType);
        }


        if (isRoot && existingDocument == null)
        {
            bool partNumberAlreadyExists = await PartNumberExistsAsync(
                documentType,
                partNumber,
                cancellationToken);
            if (partNumberAlreadyExists)
            {
                throw new PdmPartNumberChangeConflictException(
                    $"入庫失敗：系統中已存在料號為 {partNumber} 的圖檔，無法重複建立。");
            }
        }

        string? material = ExtractProperty(parseResult, "Material");
        string? designer = ExtractProperty(parseResult, "Designer", "DesignedBy", "Author");

        Dictionary<string, IngestedCadNode?> childNodesByPath = new(StringComparer.OrdinalIgnoreCase);

        if (ingestReferencedFiles &&
            !createNewDocumentForPartNumberChange &&
            (parseResult.DocumentType == SolidWorksDocumentKind.Assembly || parseResult.DocumentType == SolidWorksDocumentKind.Drawing))
        {
            foreach (string referencedPath in parseResult.ReferencedFilePaths)
            {
                string childPath = Path.GetFullPath(referencedPath);

                try
                {
                    if (!File.Exists(childPath))
                    {
                        issues.Add($"Referenced file was not found during ingest: {childPath}");
                        childNodesByPath[childPath] = null;
                        continue;
                    }

                    IngestedCadNode childNode = await IngestFileInternalAsync(
                        childPath,
                        ingestReferencedFiles,
                        effectiveSearchPaths,
                        documentManager,
                        cache,
                        issues,
                        false, // not root
                        null, // referenced files are matched by their own CAD identity
                        false, // referenced files never inherit the root identity-change action
                        uploadedBy,
                        changeReason,
                        cancellationToken);

                    childNodesByPath[childPath] = childNode;
                }
                catch (Exception ex) when (IsRecoverableReferenceIngestException(ex))
                {
                    issues.Add($"Referenced file ingest failed for '{childPath}': {ex.Message}");
                    childNodesByPath[childPath] = null;
                }
            }
        }

        string rawFileName = Path.GetFileName(normalizedPath);
        string originalFileName = rawFileName;
        // 如果是 Web 上傳的檔案，檔名會帶有 GUID 前綴 (36位 GUID + 底線)，在此將其還原
        if (rawFileName.Length > 37 && rawFileName[36] == '_' && Guid.TryParse(rawFileName.Substring(0, 36), out _))
        {
            originalFileName = rawFileName.Substring(37);
        }

        bool createdDocument = existingDocument is null;
        string normalizedExtension = NormalizeExtension(normalizedPath);

        PdmDocument document = existingDocument ?? new PdmDocument
        {
            FileName = Path.GetFileNameWithoutExtension(originalFileName),
            FileExtension = normalizedExtension,
            DocumentType = documentType,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        document.FileName = Path.GetFileNameWithoutExtension(originalFileName);
        document.FileExtension = normalizedExtension;
        document.DocumentType = documentType;
        document.PartNumber = partNumber;
        document.RevisionLabel = revision;
        document.Material = material;
        document.Designer = designer;
        document.IsActive = true;
        document.UpdatedAt = DateTimeOffset.UtcNow;
        
        // 入庫成功：自動解鎖
        document.CheckedOutBy = null;
        document.CheckedOutAt = null;

        if (createdDocument)
        {
            _dbContext.Documents.Add(document);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        int nextVersionNo = await _dbContext.DocumentVersions
            .Where(x => x.DocumentId == document.DocumentId)
            .Select(x => (int?)x.VersionNo)
            .MaxAsync(cancellationToken) + 1 ?? 1;

        string checksumSha256;
        using (var stream = File.OpenRead(normalizedPath))
        using (var sha256 = SHA256.Create())
        {
            byte[] hash = await sha256.ComputeHashAsync(stream, cancellationToken);
            checksumSha256 = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        string storageFileId;
        var existingVersionStorageId = await _dbContext.DocumentVersions
            .Where(x => x.ChecksumSha256 == checksumSha256)
            .Select(x => x.StorageFileId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(existingVersionStorageId))
        {
            storageFileId = existingVersionStorageId;
            _logger.LogInformation("File dedup matched for {Path}, reusing storage file {StorageFileId}", normalizedPath, storageFileId);
        }
        else
        {
            storageFileId = await _localStorageService.UploadFileAsync(normalizedPath, documentType, cancellationToken);
        }

        string? thumbnailStorageId = null;
        if (parseResult.ThumbnailData is { Length: > 0 } thumbnailData)
        {
            string thumbnailFileName = $"{Path.GetFileNameWithoutExtension(originalFileName)}_thumbnail.png";
            thumbnailStorageId = await _localStorageService.UploadBytesAsync(
                thumbnailData,
                thumbnailFileName,
                "Thumbnails",
                cancellationToken);
        }

        PdmDocumentVersion version = new()
        {
            DocumentId = document.DocumentId,
            VersionNo = nextVersionNo,
            RevisionLabel = revision,
            ChangeReason = string.IsNullOrWhiteSpace(changeReason) ? null : changeReason,
            StorageFileId = storageFileId,
            ThumbnailStorageId = thumbnailStorageId,
            OriginalFileName = originalFileName,
            SourceFilePath = normalizedPath,
            VaultRelativePath = BuildVaultRelativePath(documentType, normalizedPath, partNumber),
            ChecksumSha256 = checksumSha256,
            FileSizeBytes = new FileInfo(normalizedPath).Length,
            SourceLastWriteUtc = File.GetLastWriteTimeUtc(normalizedPath),
            ParsedAt = DateTimeOffset.UtcNow,
            LifecycleState = "WIP",
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.DocumentVersions.Add(version);
        await _dbContext.SaveChangesAsync(cancellationToken);

        document.CurrentVersionId = version.VersionId;
        _dbContext.Documents.Update(document);

        await ReplaceCustomPropertiesAsync(version.VersionId, parseResult, cancellationToken);
        await ReplaceBomRowsAsync(version.VersionId, parseResult, childNodesByPath, issues, cancellationToken);

        if (identityChangeSource is not null && identityChangeSourceVersionId.HasValue)
        {
            PdmDocumentIdentityChange identityChange = new()
            {
                SourceDocumentId = identityChangeSource.DocumentId,
                SourceVersionId = identityChangeSourceVersionId.Value,
                TargetDocumentId = document.DocumentId,
                OldPartNumber = identityChangeSource.PartNumber!.Trim(),
                NewPartNumber = partNumber,
                ChangeReason = changeReason!.Trim(),
                ChangedBy = uploadedBy!.Trim(),
                CreatedAt = DateTimeOffset.UtcNow
            };
            _dbContext.DocumentIdentityChanges.Add(identityChange);

            // 分支完全成功後才解除原文件鎖；例外會由外層交易回滾並保留原鎖。
            identityChangeSource.CheckedOutBy = null;
            identityChangeSource.CheckedOutAt = null;
            identityChangeSource.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        IngestedCadNode result = new(
            SourceFilePath: normalizedPath,
            DocumentId: document.DocumentId,
            VersionId: version.VersionId,
            DocumentType: documentType,
            PartNumber: partNumber,
            StorageFileId: storageFileId,
            CreatedDocument: createdDocument,
            VersionNo: nextVersionNo);

        cache[normalizedPath] = result;

        _logger.LogInformation(
            "Ingested CAD file {SourceFilePath} as DocumentId={DocumentId}, VersionId={VersionId}",
            normalizedPath,
            document.DocumentId,
            version.VersionId);

        return result;
    }

    private async Task<PdmDocument?> FindDocumentForIngestAsync(
        string documentType,
        string? partNumber,
        string sourceFilePath,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(partNumber))
        {
            PdmDocument? byPartNumber = await _dbContext.Documents
                .SingleOrDefaultAsync(
                    x => x.DocumentType == documentType && x.PartNumber == partNumber,
                    cancellationToken);

            if (byPartNumber is not null)
            {
                return byPartNumber;
            }
        }

        return await _dbContext.DocumentVersions
            .Where(x => x.SourceFilePath == sourceFilePath && x.Document.DocumentType == documentType)
            .OrderByDescending(x => x.VersionNo)
            .Select(x => x.Document)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private Task<bool> PartNumberExistsAsync(
        string documentType,
        string partNumber,
        CancellationToken cancellationToken)
    {
        string normalizedPartNumber = partNumber.Trim().ToUpperInvariant();
        return _dbContext.Documents.AnyAsync(
            x => x.DocumentType == documentType &&
                 x.PartNumber != null &&
                 x.PartNumber.Trim().ToUpper() == normalizedPartNumber,
            cancellationToken);
    }

    private async Task ReplaceCustomPropertiesAsync(
        long versionId,
        SolidWorksParseResult parseResult,
        CancellationToken cancellationToken)
    {
        List<PdmCustomProperty> properties = new();

        foreach ((string propertyName, SolidWorksCustomProperty property) in parseResult.DocumentProperties)
        {
            properties.Add(new PdmCustomProperty
            {
                VersionId = versionId,
                ConfigurationName = string.Empty,
                PropertyName = propertyName,
                PropertyValue = property.Value,
                PropertyType = property.PropertyType,
                RawExpression = null,
                IsResolved = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        foreach ((string configurationName, IReadOnlyDictionary<string, SolidWorksCustomProperty> configurationProperties) in parseResult.ConfigurationProperties)
        {
            foreach ((string propertyName, SolidWorksCustomProperty property) in configurationProperties)
            {
                properties.Add(new PdmCustomProperty
                {
                    VersionId = versionId,
                    ConfigurationName = configurationName,
                    PropertyName = propertyName,
                    PropertyValue = property.Value,
                    PropertyType = property.PropertyType,
                    RawExpression = null,
                    IsResolved = true,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        if (properties.Count > 0)
        {
            _dbContext.CustomProperties.AddRange(properties);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ReplaceBomRowsAsync(
        long parentVersionId,
        SolidWorksParseResult parseResult,
        IReadOnlyDictionary<string, IngestedCadNode?> childNodesByPath,
        ICollection<string> issues,
        CancellationToken cancellationToken)
    {
        List<PdmBomOccurrence> bomRows = new();

        if (parseResult.DocumentType == SolidWorksDocumentKind.Assembly || parseResult.DocumentType == SolidWorksDocumentKind.Drawing)
        {
            if (parseResult.DocumentType == SolidWorksDocumentKind.Drawing && parseResult.ReferencedFilePaths.Count == 0)
            {
                string? drawingPartNumber = ExtractProperty(parseResult, "PartNumber", "Number", "Part No", "PartNo", "品號");
                if (!string.IsNullOrWhiteSpace(drawingPartNumber))
                {
                    PdmDocumentVersion? matchedModelVersion = await _dbContext.DocumentVersions
                        .Where(x =>
                            x.Document.PartNumber == drawingPartNumber &&
                            x.Document.DocumentType != "Drawing" &&
                            x.Document.CurrentVersionId == x.VersionId)
                        .OrderByDescending(x => x.VersionNo)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (matchedModelVersion is not null)
                    {
                        bomRows.Add(new PdmBomOccurrence
                        {
                            ParentVersionId = parentVersionId,
                            ChildVersionId = matchedModelVersion.VersionId,
                            OccurrencePath = $"1:part-number:{drawingPartNumber}",
                            ParentConfigurationName = string.Empty,
                            ChildConfigurationName = string.Empty,
                            Quantity = 1m,
                            FindNumber = null,
                            SourceReferencePath = matchedModelVersion.SourceFilePath,
                            PackageRelativePath = matchedModelVersion.OriginalFileName,
                            ReferenceStatus = "Resolved",
                            IsSuppressed = false,
                            IsVirtual = false,
                            CreatedAt = DateTimeOffset.UtcNow
                        });
                    }
                    else
                    {
                        issues.Add($"Drawing reference fallback could not find a current model with PartNumber '{drawingPartNumber}'.");
                    }
                }
            }

            for (int index = 0; index < parseResult.ReferencedFilePaths.Count; index++)
            {
                string referencePath = Path.GetFullPath(parseResult.ReferencedFilePaths[index]);
                childNodesByPath.TryGetValue(referencePath, out IngestedCadNode? childNode);

                if (childNode is null)
                {
                    // 1. 先嘗試用絕對路徑匹配 (如果檔案存在於伺服器上)
                    if (File.Exists(referencePath))
                    {
                        PdmDocumentVersion? existingChildVersion = await _dbContext.DocumentVersions
                            .Where(x => x.SourceFilePath == referencePath)
                            .OrderByDescending(x => x.VersionNo)
                            .FirstOrDefaultAsync(cancellationToken);

                        if (existingChildVersion is not null)
                        {
                            childNode = new IngestedCadNode(
                                SourceFilePath: referencePath,
                                DocumentId: existingChildVersion.DocumentId,
                                VersionId: existingChildVersion.VersionId,
                                DocumentType: string.Empty,
                                PartNumber: null,
                                StorageFileId: existingChildVersion.StorageFileId,
                                CreatedDocument: false,
                                VersionNo: existingChildVersion.VersionNo);
                        }
                    }

                    // 2. 備案：如果絕對路徑不匹配（常見於 Web 上傳或路徑變更），則嘗試用檔名匹配
                    if (childNode is null)
                    {
                        string fileNameLookup = Path.GetFileName(referencePath).ToLower();
                        PdmDocumentVersion? existingChildVersion = await _dbContext.DocumentVersions
                            .Where(x => x.OriginalFileName.ToLower() == fileNameLookup)
                            .OrderByDescending(x => x.VersionNo)
                            .FirstOrDefaultAsync(cancellationToken);

                        if (existingChildVersion is not null)
                        {
                            childNode = new IngestedCadNode(
                                SourceFilePath: existingChildVersion.SourceFilePath,
                                DocumentId: existingChildVersion.DocumentId,
                                VersionId: existingChildVersion.VersionId,
                                DocumentType: string.Empty,
                                PartNumber: null,
                                StorageFileId: existingChildVersion.StorageFileId,
                                CreatedDocument: false,
                                VersionNo: existingChildVersion.VersionNo);
                        }
                    }
                }

                string referenceStatus = childNode is not null ? "Resolved" : "Missing";

                if (childNode is null)
                {
                    issues.Add($"BOM reference was recorded as missing: {referencePath}");
                }

                bomRows.Add(new PdmBomOccurrence
                {
                    ParentVersionId = parentVersionId,
                    ChildVersionId = childNode?.VersionId,
                    OccurrencePath = $"{index + 1}:{referencePath}",
                    ParentConfigurationName = string.Empty,
                    ChildConfigurationName = string.Empty,
                    Quantity = 1m,
                    FindNumber = null,
                    SourceReferencePath = referencePath,
                    PackageRelativePath = Path.GetFileName(referencePath),
                    ReferenceStatus = referenceStatus,
                    IsSuppressed = false,
                    IsVirtual = false,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        if (bomRows.Count > 0)
        {
            _dbContext.BomOccurrences.AddRange(bomRows);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static string MapDocumentType(SolidWorksDocumentKind documentKind)
    {
        return documentKind switch
        {
            SolidWorksDocumentKind.Part => "Part",
            SolidWorksDocumentKind.Assembly => "Assembly",
            SolidWorksDocumentKind.Drawing => "Drawing",
            _ => throw new NotSupportedException($"Unsupported SolidWorks document kind: {documentKind}.")
        };
    }

    private static string BuildVaultRelativePath(string documentType, string sourceFilePath, string? partNumber)
    {
        string fileName = Path.GetFileName(sourceFilePath);
        string safePartNumber = string.IsNullOrWhiteSpace(partNumber)
            ? "_unclassified"
            : SanitizePathSegment(partNumber);

        return Path.Combine(documentType, safePartNumber, fileName).Replace('\\', '/');
    }

    private static string SanitizePathSegment(string value)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        char[] buffer = value
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray();

        return new string(buffer);
    }

    private static string? ExtractProperty(
        SolidWorksParseResult parseResult,
        params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (parseResult.DocumentProperties.TryGetValue(propertyName, out SolidWorksCustomProperty? documentProperty)
                && !string.IsNullOrWhiteSpace(documentProperty.Value))
            {
                return documentProperty.Value;
            }

            foreach (IReadOnlyDictionary<string, SolidWorksCustomProperty> configurationProperties in parseResult.ConfigurationProperties.Values)
            {
                if (configurationProperties.TryGetValue(propertyName, out SolidWorksCustomProperty? configurationProperty)
                    && !string.IsNullOrWhiteSpace(configurationProperty.Value))
                {
                    return configurationProperty.Value;
                }
            }
        }

        return null;
    }

    private static void EnsureCheckoutLockAllowsIngest(
        PdmDocument document,
        string fileName,
        string? uploadedBy)
    {
        if (string.IsNullOrWhiteSpace(document.CheckedOutBy))
        {
            throw new InvalidOperationException(
                $"圖檔已存在系統中 (料號: {document.PartNumber})。為避免版本覆蓋與管理錯亂，請先執行『出庫 (Check-out)』後，再進行上傳更新。");
        }


        if (string.IsNullOrWhiteSpace(uploadedBy) ||
            !string.Equals(document.CheckedOutBy, uploadedBy, StringComparison.OrdinalIgnoreCase))
        {
            throw new PdmCheckoutConflictException(
                fileName,
                document.CheckedOutBy,
                document.CheckedOutAt,
                uploadedBy);
        }

        if (document.CheckedOutAt is null)
        {
            throw new PdmCheckoutConflictException(
                fileName,
                document.CheckedOutBy,
                document.CheckedOutAt,
                uploadedBy,
                "出庫鎖缺少有效時間資訊，請先復原出庫後重新出庫。");
        }
    }

    private static bool IsRecoverableReferenceIngestException(Exception ex)
    {
        return ex is FileNotFoundException or NotSupportedException
            || ex is InvalidOperationException
                and not PdmCheckoutConflictException
                and not PdmIngestIdentityMismatchException;
    }

    private static void EnsureSupportedExtension(string filePath)
    {
        string extension = NormalizeExtension(filePath);
        if (!SupportedExtensions.Contains(extension))
        {
            throw new NotSupportedException(
                $"Unsupported SolidWorks file type: '{Path.GetExtension(filePath)}'. Supported types: .sldprt, .sldasm, .slddrw.");
        }
    }

    private static string NormalizeExtension(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant();
    }

    private sealed record IngestedCadNode(
        string SourceFilePath,
        long DocumentId,
        long VersionId,
        string DocumentType,
        string? PartNumber,
        string StorageFileId,
        bool CreatedDocument,
        int VersionNo);
}

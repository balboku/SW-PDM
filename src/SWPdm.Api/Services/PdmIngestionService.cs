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
    private readonly ICadIngestionSessionFactory _solidWorksDocumentManagerServiceFactory;
    private readonly ILogger<PdmIngestionService> _logger;
    private readonly HashSet<string> _activePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _newStorageIds = new();
    private IReadOnlySet<string>? _allowedReferencePaths;

    public PdmIngestionService(
        PdmDbContext dbContext,
        IOptions<LocalStorageOptions> localStorageOptions,
        IOptions<SolidWorksDocumentManagerOptions> solidWorksOptions,
        LocalStorageService localStorageService,
        ICadIngestionSessionFactory solidWorksDocumentManagerServiceFactory,
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
        CancellationToken cancellationToken = default,
        IReadOnlySet<string>? allowedReferencePaths = null)
    {
        _activePaths.Clear();
        _newStorageIds.Clear();
        _allowedReferencePaths = allowedReferencePaths;
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

        using ICadIngestionSession documentManager = _solidWorksDocumentManagerServiceFactory.Open();

        List<string> issues = new();
        Dictionary<string, IngestedCadNode> cache = new(StringComparer.OrdinalIgnoreCase);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        bool commitAttempted = false;
        try
        {
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

            commitAttempted = true;
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
                    VersionNo: x.VersionNo,
                    Outcome: x.Outcome))
                .ToArray();

            // Existing BOMs are immutable: new uploads must never heal historical links by filename.

            return new IngestCadFileResponse(
                RootDocumentId: root.DocumentId,
                RootVersionId: root.VersionId,
                RootDocumentType: root.DocumentType,
                RootStorageFileId: root.StorageFileId,
                ProcessedFileCount: files.Count,
                Files: files,
                Issues: issues,
                PartNumberChange: partNumberChange,
                RootOutcome: root.Outcome);
        }
        catch
        {
            try { await transaction.RollbackAsync(CancellationToken.None); }
            catch (Exception rollbackError) { _logger.LogWarning(rollbackError, "Ingest rollback could not be confirmed."); }
            _dbContext.ChangeTracker.Clear();
            // A failed COMMIT can have an unknown outcome. Never delete possibly committed files.
            if (!commitAttempted)
                foreach (string storageId in _newStorageIds)
                    try
                    {
                        // Only IDs returned by this request's uploads, never a deduplicated existing file.
                        string file = _localStorageService.GetFilePath(storageId);
                        File.Delete(file);
                        string directory = Path.GetDirectoryName(file)!;
                        if (!Directory.EnumerateFileSystemEntries(directory).Any())
                            Directory.Delete(directory, false);
                    }
                    catch (Exception cleanupError) { _logger.LogWarning(cleanupError, "Uncommitted upload cleanup failed."); }
            throw;
        }
    }

    private async Task<IngestedCadNode> IngestFileInternalAsync(
        string filePath,
        bool ingestReferencedFiles,
        IReadOnlyCollection<string> inheritedSearchPaths,
        ICadIngestionSession documentManager,
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
        if (!_activePaths.Add(normalizedPath))
            throw new PdmWorkflowException("REFERENCE_CYCLE", "參照出現循環，請檢查組合件結構。");

        List<string> effectiveSearchPaths = inheritedSearchPaths.ToList();
        string? folder = Path.GetDirectoryName(normalizedPath);
        if (!string.IsNullOrWhiteSpace(folder) && !effectiveSearchPaths.Contains(folder, StringComparer.OrdinalIgnoreCase))
        {
            effectiveSearchPaths.Add(folder);
        }

        SolidWorksParseResult parseResult = documentManager.Parse(normalizedPath, effectiveSearchPaths);
        string documentType = MapDocumentType(parseResult.DocumentType);
        string? partNumber = RequiredUnambiguousProperty(parseResult, CadPropertyResolver.PartNumberNames);

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
        
        string? revision = RequiredUnambiguousProperty(parseResult, CadPropertyResolver.RevisionNames);
        string rawFileName = Path.GetFileName(normalizedPath);
        string originalFileName = rawFileName.Length > 37 && rawFileName[36] == '_' &&
            Guid.TryParse(rawFileName[..36], out _) ? rawFileName[37..] : rawFileName;
        string checksumSha256;
        await using (var stream = File.OpenRead(normalizedPath))
            checksumSha256 = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();

        // Explicit check-in must validate ownership even when the content is unchanged.
        if (isRoot && targetDocumentId.HasValue && existingDocument != null)
            EnsureCheckoutLockAllowsIngest(existingDocument, originalFileName, uploadedBy);

        PdmDocumentVersion? previous = existingDocument?.CurrentVersionId is long currentId
            ? await _dbContext.DocumentVersions.SingleOrDefaultAsync(v => v.VersionId == currentId && v.DocumentId == existingDocument!.DocumentId, cancellationToken) : null;
        if (previous != null && string.Equals(previous.ChecksumSha256, checksumSha256, StringComparison.OrdinalIgnoreCase) &&
            ((!isRoot || !targetDocumentId.HasValue) ||
             string.Equals(previous.OriginalFileName, originalFileName, StringComparison.OrdinalIgnoreCase)))
        {
            // A no-op is a read of the existing version, never a check-in/unlock of somebody else's work.
            var reused = new IngestedCadNode(normalizedPath, existingDocument!.DocumentId, previous.VersionId,
                documentType, partNumber, previous.StorageFileId, false, previous.VersionNo, "Reused");
            cache[normalizedPath] = reused;
            _activePaths.Remove(normalizedPath);
            issues.Add($"{originalFileName}：內容未變更，沿用 Ver. {previous.VersionNo}；出庫鎖保持不變，需要取消修改請使用復原出庫。");
            return reused;
        }

        if (existingDocument != null)
        {
            if (!isRoot || !targetDocumentId.HasValue)
                throw new PdmWorkflowException("EXISTING_FILE_CHANGED",
                    $"{originalFileName} 已有品號 {partNumber}，內容不同。請從 Vault 選取該文件出庫並 Check-in；批次匯入不會覆寫共用料。");
            _logger.LogWarning("Found existing document ID {DocumentId} for partNumber {PartNumber}. Checking checkout lock...", existingDocument.DocumentId, partNumber);
            EnsureCheckoutLockAllowsIngest(existingDocument, Path.GetFileName(normalizedPath), uploadedBy);
            ValidateVersionChange(previous, revision, changeReason);
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
            foreach (string referencedPath in parseResult.ReferenceDetails.Where(r => r.CanIngest)
                .Select(r => r.Path).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string childPath = Path.GetFullPath(referencedPath);

                try
                {
                    if (_allowedReferencePaths != null && !_allowedReferencePaths.Contains(childPath))
                    {
                        issues.Add($"{Path.GetFileName(childPath)} 不在本次上傳範圍，只比對庫內版本，不自動匯入伺服器其他檔案。");
                        childNodesByPath[childPath] = null;
                        continue;
                    }
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
                    // Never commit partially-tracked descendants after a child ingest failed.
                    // A missing file above remains a visible missing link; a present but invalid file aborts this root.
                    throw new PdmWorkflowException("REFERENCE_INGEST_FAILED",
                        $"參照檔案 {Path.GetFileName(childPath)} 入庫失敗，本次根文件未寫入：{ex.Message}");
                }
            }
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
            _newStorageIds.Add(storageFileId);
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
            _newStorageIds.Add(thumbnailStorageId);
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
        await ReplaceBomRowsAsync(version.VersionId, parseResult, childNodesByPath, documentManager,
            effectiveSearchPaths, issues, cancellationToken);

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
        _activePaths.Remove(normalizedPath);

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
            string normalizedNumber = partNumber.Trim().ToUpperInvariant();
            var candidates = await _dbContext.Documents.Where(
                    x => x.DocumentType == documentType && x.PartNumber != null &&
                         x.PartNumber.Trim().ToUpper() == normalizedNumber)
                .Take(2).ToListAsync(cancellationToken);
            if (candidates.Count > 1)
                throw new PdmWorkflowException("DUPLICATE_IDENTITY", "同品號與類型存在多個文件，請先確認重複身分，不能自動選取。");
            PdmDocument? byPartNumber = candidates.SingleOrDefault();

            if (byPartNumber is not null)
            {
                return byPartNumber;
            }
        }

        return null; // A reused upload path is not document identity.
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
        ICadIngestionSession cad,
        IReadOnlyCollection<string> searchPaths,
        ICollection<string> issues,
        CancellationToken cancellationToken)
    {
        var resolver = new CadReferenceResolver(_dbContext);
        var rows = new List<PdmBomOccurrence>();
        foreach (var dependency in parseResult.ReferenceDetails.Where(r => !r.IsBom))
            issues.Add($"{Path.GetFileName(dependency.Path)} 為設計／未分類參照（{dependency.NativeStatus}），已保留解析證據但不建立 BOM；完整套件仍需確認此依賴。");
        var bomReferences = parseResult.ReferenceDetails.Where(r => r.IsBom).ToArray();
        for (int index = 0; index < bomReferences.Length; index++)
        {
            var reference = bomReferences[index];
            string path = Path.GetFullPath(reference.Path);
            childNodesByPath.TryGetValue(path, out var child);
            var match = reference.IsVirtual == true
                ? new CadReferenceMatch(null, "Virtual", "NativeComponent", null)
                : reference.IsSuppressed == true
                ? new CadReferenceMatch(null, "Missing", "NativeSuppressedNotResolved", null)
                : child != null
                ? new CadReferenceMatch(child.VersionId, "Resolved", "IngestedIdentity", null)
                : await resolver.ResolveAsync(path, cad, searchPaths, cancellationToken);
            if (match.Issue != null) issues.Add(match.Issue);
            rows.Add(new PdmBomOccurrence
            {
                ParentVersionId = parentVersionId,
                ChildVersionId = match.VersionId,
                OccurrencePath = $"{index + 1}:{reference.OccurrenceName}:{path}",
                ParentConfigurationName = reference.ParentConfiguration,
                ChildConfigurationName = reference.ChildConfiguration,
                IsVirtual = reference.IsVirtual == true,
                IsSuppressed = reference.IsSuppressed == true,
                SourceReferencePath = path,
                PackageRelativePath = Path.GetFileName(path),
                ReferenceStatus = match.Status,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        // No fabricated same-part-number BOM when the CAD reports no internal reference.
        // The UI may suggest candidates, but a real relationship needs CAD evidence.
        if (parseResult.DocumentType == SolidWorksDocumentKind.Drawing && rows.Count == 0)
            issues.Add("此工程圖未解析到模型參照；請在 SolidWorks 檢查視圖參照後重新儲存。");
        _dbContext.BomOccurrences.AddRange(rows);
        await _dbContext.SaveChangesAsync(cancellationToken);
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

    private static string? RequiredUnambiguousProperty(SolidWorksParseResult parsed, string[] names)
    {
        var property = CadPropertyResolver.Resolve(parsed, names);
        if (property.Conflicts.Count > 0)
            throw new PdmWorkflowException("PROPERTY_CONFLICT", $"{names[0]} 屬性有衝突：{string.Join(" / ", property.Conflicts)}。請先統一 CAD 屬性後重試。");
        return property.Value;
    }

    public static void ValidateVersionChange(PdmDocumentVersion? previous, string? revision, string? reason)
    {
        if (previous == null) throw new PdmWorkflowException("MISSING_CURRENT_VERSION", "文件缺少目前版本，請由管理者檢查。");
        if (previous.LifecycleState is not ("WIP" or "Released"))
            throw new PdmWorkflowException("LIFECYCLE_BLOCKED", "審核中或已作廢文件不能直接入庫；請先走受控狀態流程。");
        if (string.IsNullOrWhiteSpace(reason))
            throw new PdmWorkflowException("CHANGE_REASON_REQUIRED", "請填寫變更原因；工程版次與系統版本會分別記錄。");
        if (!string.IsNullOrWhiteSpace(previous.RevisionLabel) && string.IsNullOrWhiteSpace(revision))
            throw new PdmWorkflowException("REVISION_REQUIRED", "原文件已有工程版次，更新時不可清空；請確認 CAD 版次屬性。");
        if (previous.LifecycleState == "Released" && (string.IsNullOrWhiteSpace(revision) ||
            string.Equals(previous.RevisionLabel?.Trim(), revision.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new PdmWorkflowException("RELEASED_REVISION_REQUIRED", "已發布文件須使用新的工程版次建立 WIP 版本，不能沿用原版次更新。");
    }

    private static void EnsureCheckoutLockAllowsIngest(
        PdmDocument document,
        string fileName,
        string? uploadedBy)
    {
        if (string.IsNullOrWhiteSpace(document.CheckedOutBy))
        {
            throw new PdmWorkflowException("CHECKOUT_REQUIRED",
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
        int VersionNo,
        string Outcome = "CreatedVersion");
}

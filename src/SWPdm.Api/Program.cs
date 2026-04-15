using System.IO.Compression;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SWPdm.Api.Configuration;
using SWPdm.Api.Contracts;
using SWPdm.Api.Services;
using SWPdm.Sample.Data;
using SWPdm.Sample.Data.Models;
using SWPdm.Sample.Data.Repositories;
using SWPdm.Sample.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

builder.Services
    .AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebFrontend",
        policy => policy
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services
    .AddOptions<LocalStorageOptions>()
    .Bind(builder.Configuration.GetSection(LocalStorageOptions.SectionName));

builder.Services
    .AddOptions<SolidWorksDocumentManagerOptions>()
    .Bind(builder.Configuration.GetSection(SolidWorksDocumentManagerOptions.SectionName));

builder.Services.AddDbContext<PdmDbContext>((serviceProvider, options) =>
{
    DatabaseOptions databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

    string provider = string.IsNullOrWhiteSpace(databaseOptions.Provider)
        ? "PostgreSql"
        : databaseOptions.Provider;

    string connectionString = !string.IsNullOrWhiteSpace(databaseOptions.ConnectionString)
        ? databaseOptions.ConnectionString
        : builder.Configuration.GetConnectionString("Pdm") ?? string.Empty;

    if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlServer(connectionString);
        return;
    }

    options.UseNpgsql(connectionString);
});

builder.Services.AddScoped<IPdmRepository, PdmRepository>();
builder.Services.AddScoped<PdmIngestionService>();
builder.Services.AddSingleton<LocalStorageService>(sp =>
{
    var options = sp.GetRequiredService<IOptions<LocalStorageOptions>>().Value;
    return new LocalStorageService(options.VaultPath, sp.GetRequiredService<ILogger<LocalStorageService>>());
});
builder.Services.AddSingleton<SolidWorksDocumentManagerServiceFactory>();

var app = builder.Build();

app.UseCors("AllowWebFrontend");

app.MapGet("/", () => Results.Ok(new
{
    service = "SWPdm.Api",
    status = "running",
    endpoints = new[]
    {
        "GET /health",
        "GET /api/config/status",
        "GET /api/database/status",
        "POST /api/database/migrate",
        "GET /api/documents/{documentId}",
        "GET /api/versions/{versionId}",
        "GET /api/versions/{versionId}/children",
        "GET /api/assemblies/{rootVersionId}/package-closure",
        "GET /api/assemblies/{rootVersionId}/download-zip",
        "POST /api/web/upload-temp",
        "POST /api/ingest/cad",
        "POST /api/storage/upload",
        "POST /api/storage/download",
        "POST /api/solidworks/parse"
    }
}));

app.MapHealthChecks("/health", new HealthCheckOptions());

app.MapGet("/api/config/status", (
    IOptions<DatabaseOptions> databaseOptions,
    IOptions<LocalStorageOptions> localStorageOptions,
    IOptions<SolidWorksDocumentManagerOptions> solidWorksOptions,
    IConfiguration configuration) =>
{
    DatabaseOptions database = databaseOptions.Value;
    LocalStorageOptions storage = localStorageOptions.Value;
    SolidWorksDocumentManagerOptions solidWorks = solidWorksOptions.Value;

    return Results.Ok(new
    {
        IsDatabaseConfigured = !string.IsNullOrWhiteSpace(database.ConnectionString)
            || !string.IsNullOrWhiteSpace(configuration.GetConnectionString("Pdm")),
        DatabaseProvider = database.Provider,
        IsLocalStorageConfigured = !string.IsNullOrWhiteSpace(storage.VaultPath),
        LocalStorageVaultPath = storage.VaultPath,
        IsSolidWorksDocumentManagerConfigured = !string.IsNullOrWhiteSpace(solidWorks.LicenseKey),
        SolidWorksReferenceSearchPaths = solidWorks.ReferenceSearchPaths
    });
});

app.MapGet("/api/database/status", async (
    IOptions<DatabaseOptions> databaseOptions,
    IPdmRepository repository,
    CancellationToken cancellationToken) =>
{
    bool isConfigured = !string.IsNullOrWhiteSpace(databaseOptions.Value.ConnectionString)
        || !string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Pdm"));

    if (!isConfigured)
    {
        return Results.Ok(new DatabaseStatusResponse(
            Provider: databaseOptions.Value.Provider,
            IsConfigured: false,
            CanConnect: false,
            ErrorMessage: "Database connection string is not configured."));
    }

    try
    {
        bool canConnect = await repository.CanConnectAsync(cancellationToken);

        return Results.Ok(new DatabaseStatusResponse(
            Provider: databaseOptions.Value.Provider,
            IsConfigured: true,
            CanConnect: canConnect,
            ErrorMessage: canConnect ? null : "Database provider was configured, but the connection test failed."));
    }
    catch (Exception ex)
    {
        return Results.Ok(new DatabaseStatusResponse(
            Provider: databaseOptions.Value.Provider,
            IsConfigured: true,
            CanConnect: false,
            ErrorMessage: ex.Message));
    }
});

app.MapPost("/api/database/migrate", async (
    PdmDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    try
    {
        string[] pendingBefore = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

        await dbContext.Database.MigrateAsync(cancellationToken);

        string[] pendingAfter = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

        return Results.Ok(new DatabaseMigrationResponse(
            Applied: pendingBefore.Length > 0,
            PendingMigrationCountBefore: pendingBefore.Length,
            PendingMigrationCountAfter: pendingAfter.Length,
            AppliedMigrations: pendingBefore));
    }
    catch (Exception ex)
    {
        return ToProblem(ex);
    }
});

app.MapGet("/api/documents/{documentId:long}", async (
    long documentId,
    IPdmRepository repository,
    CancellationToken cancellationToken) =>
{
    try
    {
        var document = await repository.GetDocumentAsync(documentId, cancellationToken);
        return document is null ? Results.NotFound() : Results.Ok(document);
    }
    catch (Exception ex)
    {
        return ToProblem(ex);
    }
});

app.MapGet("/api/versions/{versionId:long}", async (
    long versionId,
    IPdmRepository repository,
    CancellationToken cancellationToken) =>
{
    try
    {
        var version = await repository.GetVersionAsync(versionId, cancellationToken);
        return version is null ? Results.NotFound() : Results.Ok(version);
    }
    catch (Exception ex)
    {
        return ToProblem(ex);
    }
});

app.MapGet("/api/versions/{versionId:long}/children", async (
    long versionId,
    IPdmRepository repository,
    CancellationToken cancellationToken) =>
{
    try
    {
        var children = await repository.GetImmediateChildrenAsync(versionId, cancellationToken);
        return Results.Ok(children);
    }
    catch (Exception ex)
    {
        return ToProblem(ex);
    }
});

app.MapGet("/api/assemblies/{rootVersionId:long}/package-closure", async (
    long rootVersionId,
    IPdmRepository repository,
    CancellationToken cancellationToken) =>
{
    try
    {
        var closure = await repository.GetPackageClosureAsync(rootVersionId, cancellationToken);
        return Results.Ok(closure);
    }
    catch (Exception ex)
    {
        return ToProblem(ex);
    }
});

app.MapPost("/api/ingest/cad", async (
    IngestCadFileRequest request,
    PdmIngestionService ingestionService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.LocalFilePath))
    {
        return ValidationError(nameof(request.LocalFilePath), "LocalFilePath is required.");
    }

    try
    {
        IngestCadFileResponse response = await ingestionService.IngestAsync(request, cancellationToken);
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return ToProblem(ex);
    }
});

app.MapPost("/api/web/upload-temp", async (
    IFormFile file,
    CancellationToken cancellationToken) =>
{
    if (file == null || file.Length == 0)
    {
        return ValidationError(nameof(file), "File is required.");
    }

    try
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "swpdm_web_uploads");
        Directory.CreateDirectory(tempDir);

        string fileName = Path.GetFileName(file.FileName);
        string tempFilePath = Path.Combine(tempDir, $"{Guid.NewGuid()}_{fileName}");

        await using var stream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(stream, cancellationToken);
        
        // 此處不關閉 stream 就回傳路徑會有問題，需確保 stream 透過 using 已被鎖定與釋放，因為 await using 會在 scope 結束時釋放。
        
        return Results.Ok(new { localFilePath = tempFilePath });
    }
    catch (Exception ex)
    {
        return ToProblem(ex);
    }
}).DisableAntiforgery(); // Disable AntiForgery for raw API usage in development

app.MapPost("/api/storage/upload", async (
    UploadFileRequest request,
    LocalStorageService storageService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.LocalFilePath))
    {
        return ValidationError(nameof(request.LocalFilePath), "LocalFilePath is required.");
    }

    try
    {
        // 為了相容端點簽名，將 DocumentType 傳入暫時代理目錄名稱（實際可只用 GUID）
        string fileId = await storageService.UploadFileAsync(request.LocalFilePath, "ManualUploads", cancellationToken);

        return Results.Ok(new UploadFileResponse(
            StorageFileId: fileId,
            LocalFilePath: request.LocalFilePath));
    }
    catch (Exception ex)
    {
        return ToProblem(ex);
    }
});

app.MapPost("/api/storage/download", async (
    DownloadFileRequest request,
    LocalStorageService storageService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.FileId))
    {
        return ValidationError(nameof(request.FileId), "FileId is required.");
    }

    if (string.IsNullOrWhiteSpace(request.DestinationFilePath))
    {
        return ValidationError(nameof(request.DestinationFilePath), "DestinationFilePath is required.");
    }

    try
    {
        string savedPath = await storageService.DownloadFileAsync(
            request.FileId,
            request.DestinationFilePath,
            cancellationToken);

        return Results.Ok(new DownloadFileResponse(
            FileId: request.FileId,
            SavedPath: savedPath));
    }
    catch (Exception ex)
    {
        return ToProblem(ex);
    }
});

// ─────────────────────────────────────────────────────────────────────
// GET /api/assemblies/{rootVersionId}/download-zip
//
// 階段 D 完整實作：
//   1. 遞迴查詢 BOM（WITH RECURSIVE CTE），取得組合件的所有依賴版本
//   2. 從 Google Drive 下載全部檔案到伺服器暫存目錄
//   3. 打包成 ZIP（flat 結構：所有檔案放在 ZIP 根目錄）
//   4. 串流回傳給瀏覽器，並在回傳前清除暫存檔
//
// ★ Flat ZIP 結構說明：
//   SolidWorks 開啟組合件時，會優先搜尋組合件所在目錄下的零件檔。
//   只要將 .sldasm 與所有 .sldprt 解壓到同一目錄，SW 就能正確解析
//   所有組件參考，不會出現「找不到參考零件」的錯誤。
// ─────────────────────────────────────────────────────────────────────
app.MapGet("/api/assemblies/{rootVersionId:long}/download-zip", async (
    long rootVersionId,
    IPdmRepository repository,
    LocalStorageService storageService,
    CancellationToken cancellationToken) =>
{
    // Step 1：遞迴查詢 BOM closure（含根版本本身）
    IReadOnlyList<PdmPackageFile> files =
        await repository.GetPackageClosureAsync(rootVersionId, cancellationToken);

    if (files.Count == 0)
    {
        return Results.NotFound(new
        {
            title = "Assembly not found",
            detail = $"No package files found for rootVersionId={rootVersionId}. " +
                     "Verify the version exists and has resolved BOM references."
        });
    }

    // Step 2：建立本次請求的隔離暫存目錄（避免並行下載衝突）
    string sessionId = Guid.NewGuid().ToString("N")[..8];
    string tempDir = Path.Combine(
        Path.GetTempPath(),
        $"swpdm_zip_{rootVersionId}_{sessionId}");
    Directory.CreateDirectory(tempDir);

    string zipPath = Path.Combine(
        Path.GetTempPath(),
        $"assembly_{rootVersionId}_{sessionId}.zip");

    try
    {
        List<string> downloadIssues = new();

        foreach (PdmPackageFile file in files)
        {
            // 對相同檔名做安全去重（depth 越小的版本優先保留）
            string safeFileName = SanitizeFileName(file.OriginalFileName);
            string destPath = Path.Combine(tempDir, safeFileName);

            // 若同名檔案已下載（例如 BOM 中有重複參考），略過
            if (File.Exists(destPath))
            {
                continue;
            }

            try
            {
                await storageService.DownloadFileAsync(
                    file.StorageFileId,
                    destPath,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                // 單一檔案下載失敗時記錄但繼續（組合件仍可能部分可用）
                downloadIssues.Add(
                    $"Failed to copy '{file.OriginalFileName}' " +
                    $"(storageId={file.StorageFileId}): {ex.Message}");
            }
        }

        // Step 4：將暫存目錄內所有已下載的檔案打包成 ZIP
        //   CompressionLevel.Fastest：SolidWorks 二進位檔案壓縮率低，
        //   優先降低 CPU 開銷而非壓縮率。
        using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            foreach (string filePath in Directory.EnumerateFiles(tempDir))
            {
                archive.CreateEntryFromFile(
                    filePath,
                    Path.GetFileName(filePath), // ZIP 內路徑 = 純檔名（flat）
                    CompressionLevel.Fastest);
            }
        }

        // Step 5：讀入 ZIP bytes，清除磁碟暫存，回傳給瀏覽器
        byte[] zipBytes = await File.ReadAllBytesAsync(zipPath, cancellationToken);

        string downloadFileName = $"assembly_{rootVersionId}.zip";

        return Results.File(
            zipBytes,
            contentType: "application/zip",
            fileDownloadName: downloadFileName);
    }
    catch (Exception ex)
    {
        return ToProblem(ex);
    }
    finally
    {
        // 確保暫存檔與目錄無論如何都被清除
        try
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);

            if (File.Exists(zipPath))
                File.Delete(zipPath);
        }
        catch
        {
            // 清除失敗不影響回應結果，下次啟動或 OS 會自行回收 /tmp
        }
    }
});

app.MapPost("/api/solidworks/parse", (
    ParseSolidWorksFileRequest request,
    IOptions<SolidWorksDocumentManagerOptions> solidWorksOptions,
    SolidWorksDocumentManagerServiceFactory documentManagerFactory) =>
{
    if (string.IsNullOrWhiteSpace(request.FilePath))
    {
        return ValidationError(nameof(request.FilePath), "FilePath is required.");
    }

    try
    {
        string[] mergedSearchPaths = solidWorksOptions.Value.ReferenceSearchPaths
            .Concat(request.AdditionalSearchPaths ?? Array.Empty<string>())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        using SolidWorksDocumentManagerService documentManager = documentManagerFactory.Create();
        SolidWorksParseResult result = documentManager.Parse(request.FilePath, mergedSearchPaths);

        return Results.Ok(new SolidWorksParseResponse(
            FilePath: result.FilePath,
            DocumentType: result.DocumentType.ToString(),
            DocumentProperties: result.DocumentProperties,
            ConfigurationProperties: result.ConfigurationProperties,
            ReferencedFilePaths: result.ReferencedFilePaths));
    }
    catch (Exception ex)
    {
        return ToProblem(ex);
    }
});

if (!EF.IsDesignTime)
{
    app.Run();
}

static IResult ValidationError(string fieldName, string message)
{
    return Results.ValidationProblem(new Dictionary<string, string[]>
    {
        [fieldName] = new[] { message }
    });
}

static IResult ToProblem(Exception ex)
{
    return ex switch
    {
        ArgumentException argumentException => Results.Problem(
            title: "Invalid request",
            detail: argumentException.Message,
            statusCode: StatusCodes.Status400BadRequest),
        FileNotFoundException fileNotFoundException => Results.Problem(
            title: "File not found",
            detail: fileNotFoundException.Message,
            statusCode: StatusCodes.Status404NotFound),
        UnauthorizedAccessException unauthorizedAccessException => Results.Problem(
            title: "Access denied",
            detail: unauthorizedAccessException.Message,
            statusCode: StatusCodes.Status403Forbidden),
        TimeoutException timeoutException => Results.Problem(
            title: "Operation timed out",
            detail: timeoutException.Message,
            statusCode: StatusCodes.Status504GatewayTimeout),
        InvalidOperationException invalidOperationException => Results.Problem(
            title: "Operation failed",
            detail: invalidOperationException.Message,
            statusCode: StatusCodes.Status500InternalServerError),
        NotSupportedException notSupportedException => Results.Problem(
            title: "Not supported",
            detail: notSupportedException.Message,
            statusCode: StatusCodes.Status501NotImplemented),
        _ => Results.Problem(
            title: "Unexpected error",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError)
    };
}

/// <summary>
/// 移除檔名中的非法字元，確保跨 OS 寫入 ZIP 時安全。
/// 例如：將 '/' '\' ':' 等替換為 '_'。
/// </summary>
static string SanitizeFileName(string fileName)
{
    char[] invalid = Path.GetInvalidFileNameChars();
    char[] buffer = Path.GetFileName(fileName)   // 僅取最後一段，去除路徑前綴
        .Select(ch => invalid.Contains(ch) ? '_' : ch)
        .ToArray();

    return new string(buffer);
}

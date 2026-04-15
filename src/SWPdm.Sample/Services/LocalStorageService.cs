namespace SWPdm.Sample.Services;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Wraps Local File System upload/download operations for SolidWorks files.
/// </summary>
public sealed class LocalStorageService
{
    private readonly string _vaultPath;
    private readonly ILogger<LocalStorageService>? _logger;

    public LocalStorageService(
        string vaultPath,
        ILogger<LocalStorageService>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(vaultPath))
        {
            throw new ArgumentException("Vault path is required.", nameof(vaultPath));
        }

        _vaultPath = Path.GetFullPath(vaultPath);
        Directory.CreateDirectory(_vaultPath);

        _logger = logger;
    }

    /// <summary>
    /// Uploads a local file to the Vault and returns the unique relative file ID.
    /// </summary>
    public async Task<string> UploadFileAsync(
        string localFilePath,
        string DocumentType, // Added argument just to keep folder structure organized if desired, though we can just generate a UUID.
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(localFilePath))
        {
            throw new ArgumentException("Local file path is required.", nameof(localFilePath));
        }

        if (!File.Exists(localFilePath))
        {
            throw new FileNotFoundException("The file to upload does not exist.", localFilePath);
        }

        // Generate a unique file ID
        string fileId = Guid.NewGuid().ToString("N");
        string extension = Path.GetExtension(localFilePath);
        
        // E.g., vault_storage/ab12cd34/part.sldprt
        string relativeFilePath = Path.Combine(fileId, Path.GetFileName(localFilePath));
        string destinationPath = Path.Combine(_vaultPath, relativeFilePath);
        
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        await using FileStream sourceStream = new(
            localFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        await using FileStream destinationStream = new(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await sourceStream.CopyToAsync(destinationStream, cancellationToken);

        _logger?.LogInformation(
            "Uploaded '{LocalFilePath}' to Local Vault. StorageFileId={FileId}",
            localFilePath,
            relativeFilePath);

        return relativeFilePath; // The relative path serves as our FileID
    }

    /// <summary>
    /// Downloads a Vault file to the specified absolute destination path.
    /// Returns the saved local path for convenience.
    /// </summary>
    public async Task<string> DownloadFileAsync(
        string storageFileId,
        string destinationFilePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageFileId))
        {
            throw new ArgumentException("Storage File ID is required.", nameof(storageFileId));
        }

        if (string.IsNullOrWhiteSpace(destinationFilePath))
        {
            throw new ArgumentException("Destination file path is required.", nameof(destinationFilePath));
        }

        string sourcePath = Path.Combine(_vaultPath, storageFileId);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"Vault file '{sourcePath}' was not found.");
        }

        string? destinationDirectory = Path.GetDirectoryName(destinationFilePath);
        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            throw new ArgumentException(
                "Destination file path must include a directory.",
                nameof(destinationFilePath));
        }

        Directory.CreateDirectory(destinationDirectory);

        await using FileStream sourceStream = new(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        await using FileStream destinationStream = new(
            destinationFilePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await sourceStream.CopyToAsync(destinationStream, cancellationToken);

        _logger?.LogInformation(
            "Downloaded Vault StorageFileId={FileId} to '{DestinationFilePath}'",
            storageFileId,
            destinationFilePath);

        return destinationFilePath;
    }
}

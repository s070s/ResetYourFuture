using Microsoft.Extensions.Configuration;
using ResetYourFuture.Application.ApiInterfaces;

namespace ResetYourFuture.Infrastructure.ApiServices;

/// <summary>
/// Local file system implementation of IFileStorage.
/// Stores files under the configured uploads directory (default ./App_Data/Uploads/{folder}/{fileName}).
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private readonly string _basePath;
    private readonly ILogger<LocalFileStorage> _logger;

    // File size limits (in bytes)
    private const long MaxAvatarSize = 5 * 1024 * 1024; // 5 MB
    private const long MaxPdfSize = 20 * 1024 * 1024; // 20 MB
    private const long MaxVideoSize = 500 * 1024 * 1024; // 500 MB
    private const long MaxBackgroundImageSize = 8 * 1024 * 1024; // 8 MB

    // Allowed content types
    private static readonly HashSet<string> AllowedImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp"
    };

    private static readonly HashSet<string> AllowedPdfTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf"
    };

    private static readonly HashSet<string> AllowedVideoTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/mp4", "video/webm", "video/ogg"
    };

    public LocalFileStorage(IWebHostEnvironment environment, IConfiguration configuration, ILogger<LocalFileStorage> logger)
    {
        // CLOUD-1: allow the uploads directory to live outside the deploy folder (so a "delete and
        // re-copy" update isn't destructive) by setting Storage:UploadsPath; default stays inside
        // the content root for zero-config local development.
        var configuredPath = configuration["Storage:UploadsPath"];
        _basePath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "Uploads")
            : configuredPath;
        _logger = logger;

        // Ensure base directory exists
        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
        }
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string folder, long? maxBytes = null, CancellationToken cancellationToken = default)
    {
        // Validate file name
        fileName = Path.GetFileName(fileName); // Security: prevent directory traversal
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name is required", nameof(fileName));
        }

        // Validate folder
        folder = folder.Replace("\\", "/").Trim('/');
        if (string.IsNullOrWhiteSpace(folder) || folder.Contains(".."))
        {
            throw new ArgumentException("Invalid folder path", nameof(folder));
        }

        // Generate unique file name to prevent collisions
        var extension = Path.GetExtension(fileName);
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var uniqueFileName = $"{fileNameWithoutExt}_{Guid.NewGuid():N}{extension}";

        // Create folder structure
        var folderPath = Path.Combine(_basePath, folder);
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        // Build full path
        var filePath = Path.Combine(folderPath, uniqueFileName);

        // Validate file size — prefer explicit cap; fall back to folder-heuristic.
        ValidateFileSize(fileStream, folder, maxBytes);

        // Save file
        using var fileStreamOut = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await fileStream.CopyToAsync(fileStreamOut, cancellationToken);

        // Return relative path
        var relativePath = $"{folder}/{uniqueFileName}";
        _logger.LogInformation("File saved: {FilePath}", relativePath);

        return relativePath;
    }

    public Task<(Stream stream, string contentType)> GetFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        // Validate and sanitize path
        filePath = filePath.Replace("\\", "/").Trim('/');
        if (string.IsNullOrWhiteSpace(filePath) || filePath.Contains(".."))
        {
            throw new ArgumentException("Invalid file path", nameof(filePath));
        }

        var fullPath = Path.Combine(_basePath, filePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("File not found", filePath);
        }

        // Open file stream
        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);

        // Determine content type from extension
        var contentType = GetContentType(Path.GetExtension(fullPath));

        return Task.FromResult((stream as Stream, contentType));
    }

    public Task DeleteFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        // Validate and sanitize path
        filePath = filePath.Replace("\\", "/").Trim('/');
        if (string.IsNullOrWhiteSpace(filePath) || filePath.Contains(".."))
        {
            throw new ArgumentException("Invalid file path", nameof(filePath));
        }

        var fullPath = Path.Combine(_basePath, filePath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("File deleted: {FilePath}", filePath);
        }

        return Task.CompletedTask;
    }

    public bool FileExists(string filePath)
    {
        // Validate and sanitize path
        filePath = filePath.Replace("\\", "/").Trim('/');
        if (string.IsNullOrWhiteSpace(filePath) || filePath.Contains(".."))
        {
            return false;
        }

        var fullPath = Path.Combine(_basePath, filePath);
        return File.Exists(fullPath);
    }

    private void ValidateFileSize(Stream fileStream, string folder, long? explicitMaxBytes)
    {
        // Network streams (e.g. from IFormFile) may not be seekable;
        // skip stream-level validation — callers should pre-check IFormFile.Length.
        if (!fileStream.CanSeek)
            return;

        var fileSize = fileStream.Length;

        // Prefer the explicit cap passed by the caller. The folder-name heuristic is the
        // last resort — new folders that don't match any keyword will throw rather than
        // silently inheriting an arbitrary default.
        long maxSize = explicitMaxBytes ?? folder.ToLowerInvariant() switch
        {
            var f when f.Contains("avatar") => MaxAvatarSize,
            var f when f.Contains("pdf") => MaxPdfSize,
            var f when f.Contains("video") => MaxVideoSize,
            var f when f.Contains("background") => MaxBackgroundImageSize,
            var f when f.Contains("cover") => MaxBackgroundImageSize,
            var f when f.Contains("blog") => MaxBackgroundImageSize,
            var f when f.Contains("certificate") => MaxPdfSize,
            _ => throw new InvalidOperationException(
                $"No file-size limit configured for folder '{folder}'. " +
                "Pass an explicit maxBytes to SaveFileAsync or add a folder mapping.")
        };

        if (fileSize > maxSize)
        {
            throw new InvalidOperationException($"File size ({fileSize} bytes) exceeds maximum allowed size ({maxSize} bytes)");
        }
    }

    private string GetContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".ogg" => "video/ogg",
            _ => "application/octet-stream"
        };
    }
}

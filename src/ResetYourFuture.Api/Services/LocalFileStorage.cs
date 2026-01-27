using ResetYourFuture.Api.Interfaces;

namespace ResetYourFuture.Api.Services;

/// <summary>
/// Local file system implementation of IFileStorage.
/// Stores files in ./App_Data/Uploads/{folder}/{fileName}.
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
    
    public LocalFileStorage(IWebHostEnvironment environment, ILogger<LocalFileStorage> logger)
    {
        _basePath = Path.Combine(environment.ContentRootPath, "App_Data", "Uploads");
        _logger = logger;
        
        // Ensure base directory exists
        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
        }
    }
    
    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string folder, CancellationToken cancellationToken = default)
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
        
        // Validate file size based on folder type
        ValidateFileSize(fileStream, folder);
        
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
    
    private void ValidateFileSize(Stream fileStream, string folder)
    {
        var fileSize = fileStream.Length;
        
        long maxSize = folder.ToLowerInvariant() switch
        {
            var f when f.Contains("avatar") => MaxAvatarSize,
            var f when f.Contains("pdf") => MaxPdfSize,
            var f when f.Contains("video") => MaxVideoSize,
            var f when f.Contains("background") => MaxBackgroundImageSize,
            _ => MaxPdfSize // Default
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

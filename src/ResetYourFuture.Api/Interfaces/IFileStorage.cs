namespace ResetYourFuture.Api.Interfaces;

/// <summary>
/// Abstraction for file storage operations.
/// Supports local file system storage (can be extended to cloud storage).
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Saves a file to storage and returns the file path.
    /// </summary>
    /// <param name="fileStream">File content stream</param>
    /// <param name="fileName">Original file name</param>
    /// <param name="folder">Folder/category for organization (e.g., "avatars", "lessons/pdf")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Relative file path for storage in database</returns>
    Task<string> SaveFileAsync(Stream fileStream, string fileName, string folder, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves a file from storage.
    /// </summary>
    /// <param name="filePath">Relative file path from database</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File stream and content type</returns>
    Task<(Stream stream, string contentType)> GetFileAsync(string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a file from storage.
    /// </summary>
    /// <param name="filePath">Relative file path from database</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteFileAsync(string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a file exists in storage.
    /// </summary>
    /// <param name="filePath">Relative file path from database</param>
    /// <returns>True if file exists, false otherwise</returns>
    bool FileExists(string filePath);
}

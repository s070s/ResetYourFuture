using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Api.Interfaces;

namespace ResetYourFuture.Api.Controllers;

/// <summary>
/// Public endpoint for serving non-sensitive uploaded media files (blog covers, testimonial avatars).
/// Restricts access to an explicit allowlist of public folders to prevent directory traversal or
/// exposure of sensitive uploads (e.g. lesson PDFs, avatars).
/// </summary>
[ApiController]
[Route( "api/media" )]
public class MediaController : ControllerBase
{
    private static readonly HashSet<string> PublicFolders = new( StringComparer.OrdinalIgnoreCase )
    {
        "blog/covers",
        "testimonials/avatars"
    };

    private readonly IFileStorage _fileStorage;

    public MediaController( IFileStorage fileStorage )
    {
        _fileStorage = fileStorage;
    }

    /// <summary>
    /// Serves an uploaded file if it lives within an allowed public folder.
    /// Example: GET /api/media/testimonials/avatars/photo_abc123.jpg
    /// </summary>
    [HttpGet( "{*filePath}" )]
    public async Task<IActionResult> GetFile( string filePath, CancellationToken cancellationToken = default )
    {
        if ( string.IsNullOrWhiteSpace( filePath ) || filePath.Contains( ".." ) )
            return BadRequest( "Invalid file path." );

        // Normalise to forward slashes
        filePath = filePath.Replace( '\\', '/' ).Trim( '/' );

        // Only serve files from explicitly allowed public folders
        var allowed = PublicFolders.Any( folder =>
            filePath.StartsWith( folder + "/", StringComparison.OrdinalIgnoreCase ) );

        if ( !allowed )
            return NotFound();

        if ( !_fileStorage.FileExists( filePath ) )
            return NotFound();

        var (stream, contentType) = await _fileStorage.GetFileAsync( filePath, cancellationToken );

        // Allow browsers to cache public media for 24 h
        Response.Headers.CacheControl = "public, max-age=86400";

        return File( stream, contentType );
    }
}

using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Web.ApiInterfaces;

namespace ResetYourFuture.Web.Controllers;

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

    // Only these extensions may be served through the public media endpoint.
    // Prevents drive-by content-type attacks where an attacker uploads a file with
    // a crafted extension (e.g. .html, .svg) to execute scripts in browser context.
    private static readonly HashSet<string> AllowedExtensions = new( StringComparer.OrdinalIgnoreCase )
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".avif", ".svg"
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

        // Reject any extension not on the explicit allowlist.
        var ext = Path.GetExtension( filePath );
        if ( string.IsNullOrEmpty( ext ) || !AllowedExtensions.Contains( ext ) )
            return NotFound();

        if ( !_fileStorage.FileExists( filePath ) )
            return NotFound();

        var (stream, contentType) = await _fileStorage.GetFileAsync( filePath, cancellationToken );

        // Allow browsers to cache public media for 24 h
        Response.Headers.CacheControl = "public, max-age=86400";
        // Prevent MIME-sniffing attacks and force inline rendering (not download)
        Response.Headers[ "X-Content-Type-Options" ] = "nosniff";
        var fileName = Path.GetFileName( filePath );
        Response.Headers[ "Content-Disposition" ] = $"inline; filename=\"{fileName}\"";

        return File( stream, contentType );
    }
}

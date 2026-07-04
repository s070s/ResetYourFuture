using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Data;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// Admin-only CRUD endpoints for blog article management.
/// </summary>
[ApiController]
[Route("api/admin/blog")]
[Authorize(Policy = "AdminOnly")]
[Tags("Admin · Blog")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public class AdminBlogController : ControllerBase
{
    private readonly IBlogArticleService _blogService;
    private readonly IFileStorage _fileStorage;
    private readonly IApplicationDbContext _context;

    public AdminBlogController(
        IBlogArticleService blogService,
        IFileStorage fileStorage,
        IApplicationDbContext context)
    {
        _blogService = blogService;
        _fileStorage = fileStorage;
        _context = context;
    }

    /// <summary>Get a paged list of all blog articles (published and draft) with optional search.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminBlogArticleDto>>> GetBlogArticles(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _blogService.GetAllForAdminAsync(page, pageSize, search, cancellationToken);
        return Ok(result);
    }

    /// <summary>Get a single blog article by id (admin view, includes drafts).</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminBlogArticleDto>> GetBlogArticle(
        Guid id, CancellationToken cancellationToken = default)
    {
        var article = await _blogService.GetByIdForAdminAsync(id, cancellationToken);
        return article is null ? NotFound() : Ok(article);
    }

    /// <summary>Create a new blog article. Returns 409 if the slug already exists.</summary>
    [HttpPost]
    [ProducesResponseType<AdminBlogArticleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminBlogArticleDto>> CreateBlogArticle(
        [FromBody] SaveBlogArticleRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _blogService.CreateAsync(request, cancellationToken);
        if (result is null)
            return Conflict("A blog article with this slug already exists.");

        return CreatedAtAction(nameof(GetBlogArticle), new { id = result.Id }, result);
    }

    /// <summary>Update an existing blog article. Returns 409 if the new slug clashes with another article.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminBlogArticleDto>> UpdateBlogArticle(
        Guid id,
        [FromBody] SaveBlogArticleRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _blogService.UpdateAsync(id, request, cancellationToken);
        if (result is null)
        {
            var exists = await _blogService.GetByIdForAdminAsync(id, cancellationToken);
            return exists is null ? NotFound() : Conflict("A blog article with this slug already exists.");
        }
        return Ok(result);
    }

    /// <summary>Publish a blog article (make it publicly visible).</summary>
    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> PublishBlogArticle(
        Guid id, CancellationToken cancellationToken = default)
    {
        var success = await _blogService.PublishAsync(id, cancellationToken);
        return success ? NoContent() : NotFound();
    }

    /// <summary>Unpublish a blog article (hide it from the public site).</summary>
    [HttpPost("{id:guid}/unpublish")]
    public async Task<IActionResult> UnpublishBlogArticle(
        Guid id, CancellationToken cancellationToken = default)
    {
        var success = await _blogService.UnpublishAsync(id, cancellationToken);
        return success ? NoContent() : NotFound();
    }

    /// <summary>Delete a blog article.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteBlogArticle(
        Guid id, CancellationToken cancellationToken = default)
    {
        var success = await _blogService.DeleteAsync(id, cancellationToken);
        return success ? NoContent() : NotFound();
    }

    /// <summary>Upload (or replace) the cover image for a blog article.</summary>
    [HttpPost("{id:guid}/upload/cover")]
    public async Task<IActionResult> UploadBlogCoverImage(
        Guid id, IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file provided.");

        var article = await _context.BlogArticles.FindAsync(new object[] { id }, cancellationToken);
        if (article is null)
            return NotFound();

        // Delete the old cover image if it was an uploaded file (not a URL).
        if (!string.IsNullOrEmpty(article.CoverImageUrl) &&
             !article.CoverImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            await _fileStorage.DeleteFileAsync(article.CoverImageUrl);
        }

        using var stream = file.OpenReadStream();
        var path = await _fileStorage.SaveFileAsync(stream, file.FileName, "blog/covers", cancellationToken: cancellationToken);

        article.CoverImageUrl = path;
        article.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new BlogCoverUploadResultDto(path));
    }
}

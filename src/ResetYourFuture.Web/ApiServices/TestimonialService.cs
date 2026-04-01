using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Web.Data;
using ResetYourFuture.Web.Domain.Entities;
using ResetYourFuture.Web.ApiInterfaces;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.ApiServices;

/// <summary>
/// Implements ITestimonialService using ApplicationDbContext.
/// </summary>
public class TestimonialService : ITestimonialService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<TestimonialService> _logger;

    public TestimonialService( ApplicationDbContext db, ILogger<TestimonialService> logger )
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AdminTestimonialDto>> GetActiveAsync(
        CancellationToken cancellationToken = default )
    {
        var items = await _db.Testimonials
            .AsNoTracking()
            .Where( t => t.IsActive )
            .OrderBy( t => t.DisplayOrder )
            .ToListAsync( cancellationToken );

        return items.Select( ToAdminDto ).ToList();
    }

    public async Task<PagedResult<AdminTestimonialDto>> GetAllForAdminAsync(
        int page, int pageSize, CancellationToken cancellationToken = default )
    {
        page = Math.Max( 1, page );
        pageSize = Math.Clamp( pageSize, 1, 100 );

        var query = _db.Testimonials.AsNoTracking();

        var total = await query.CountAsync( cancellationToken );

        var items = await query
            .OrderBy( t => t.DisplayOrder )
            .ThenBy( t => t.CreatedAt )
            .Skip( ( page - 1 ) * pageSize )
            .Take( pageSize )
            .ToListAsync( cancellationToken );

        return new PagedResult<AdminTestimonialDto>(
            Items: items.Select( ToAdminDto ).ToList(),
            TotalCount: total,
            Page: page,
            PageSize: pageSize,
            SortBy: "displayOrder",
            SortDir: "asc" );
    }

    public async Task<AdminTestimonialDto?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default )
    {
        var item = await _db.Testimonials
            .AsNoTracking()
            .FirstOrDefaultAsync( t => t.Id == id, cancellationToken );

        return item is null ? null : ToAdminDto( item );
    }

    public async Task<AdminTestimonialDto> CreateAsync(
        SaveTestimonialRequest request, CancellationToken cancellationToken = default )
    {
        // Assign display order: place at end by default (max + 1)
        var maxOrder = await _db.Testimonials
            .AnyAsync( cancellationToken )
            ? await _db.Testimonials.MaxAsync( t => t.DisplayOrder, cancellationToken )
            : 0;

        var now = DateTimeOffset.UtcNow;

        var testimonial = new Testimonial
        {
            Id             = Guid.NewGuid(),
            FullName       = request.FullName,
            RoleOrTitle    = request.RoleOrTitle,
            CompanyOrContext = request.CompanyOrContext,
            QuoteText      = request.QuoteText,
            DisplayOrder   = request.DisplayOrder > 0 ? request.DisplayOrder : maxOrder + 1,
            IsActive       = request.IsActive,
            CreatedAt      = now
        };

        _db.Testimonials.Add( testimonial );
        await _db.SaveChangesAsync( cancellationToken );

        _logger.LogInformation( "Testimonial created: {Id} '{FullName}'.", testimonial.Id, testimonial.FullName );
        return ToAdminDto( testimonial );
    }

    public async Task<AdminTestimonialDto?> UpdateAsync(
        Guid id, SaveTestimonialRequest request, CancellationToken cancellationToken = default )
    {
        var testimonial = await _db.Testimonials
            .FirstOrDefaultAsync( t => t.Id == id, cancellationToken );

        if ( testimonial is null )
            return null;

        testimonial.FullName         = request.FullName;
        testimonial.RoleOrTitle      = request.RoleOrTitle;
        testimonial.CompanyOrContext  = request.CompanyOrContext;
        testimonial.QuoteText        = request.QuoteText;
        testimonial.DisplayOrder     = request.DisplayOrder;
        testimonial.IsActive         = request.IsActive;
        testimonial.UpdatedAt        = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync( cancellationToken );

        _logger.LogInformation( "Testimonial updated: {Id} '{FullName}'.", testimonial.Id, testimonial.FullName );
        return ToAdminDto( testimonial );
    }

    public async Task<AdminTestimonialDto?> ToggleActiveAsync(
        Guid id, CancellationToken cancellationToken = default )
    {
        var testimonial = await _db.Testimonials
            .FirstOrDefaultAsync( t => t.Id == id, cancellationToken );

        if ( testimonial is null )
            return null;

        testimonial.IsActive  = !testimonial.IsActive;
        testimonial.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync( cancellationToken );
        return ToAdminDto( testimonial );
    }

    public async Task<bool> MoveUpAsync( Guid id, CancellationToken cancellationToken = default )
    {
        var current = await _db.Testimonials
            .FirstOrDefaultAsync( t => t.Id == id, cancellationToken );

        if ( current is null )
            return false;

        // Find the item with the next-lower DisplayOrder
        var previous = await _db.Testimonials
            .Where( t => t.DisplayOrder < current.DisplayOrder )
            .OrderByDescending( t => t.DisplayOrder )
            .FirstOrDefaultAsync( cancellationToken );

        if ( previous is null )
            return false;

        ( current.DisplayOrder, previous.DisplayOrder ) = ( previous.DisplayOrder, current.DisplayOrder );
        current.UpdatedAt  = DateTimeOffset.UtcNow;
        previous.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync( cancellationToken );
        return true;
    }

    public async Task<bool> MoveDownAsync( Guid id, CancellationToken cancellationToken = default )
    {
        var current = await _db.Testimonials
            .FirstOrDefaultAsync( t => t.Id == id, cancellationToken );

        if ( current is null )
            return false;

        // Find the item with the next-higher DisplayOrder
        var next = await _db.Testimonials
            .Where( t => t.DisplayOrder > current.DisplayOrder )
            .OrderBy( t => t.DisplayOrder )
            .FirstOrDefaultAsync( cancellationToken );

        if ( next is null )
            return false;

        ( current.DisplayOrder, next.DisplayOrder ) = ( next.DisplayOrder, current.DisplayOrder );
        current.UpdatedAt = DateTimeOffset.UtcNow;
        next.UpdatedAt    = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync( cancellationToken );
        return true;
    }

    public async Task<AdminTestimonialDto?> SetAvatarPathAsync(
        Guid id, string path, CancellationToken cancellationToken = default )
    {
        var testimonial = await _db.Testimonials
            .FirstOrDefaultAsync( t => t.Id == id, cancellationToken );

        if ( testimonial is null )
            return null;

        testimonial.AvatarPath = path;
        testimonial.UpdatedAt  = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync( cancellationToken );
        return ToAdminDto( testimonial );
    }

    public async Task<bool> RemoveAvatarAsync( Guid id, CancellationToken cancellationToken = default )
    {
        var testimonial = await _db.Testimonials
            .FirstOrDefaultAsync( t => t.Id == id, cancellationToken );

        if ( testimonial is null )
            return false;

        testimonial.AvatarPath = null;
        testimonial.UpdatedAt  = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync( cancellationToken );
        return true;
    }

    public async Task<bool> DeleteAsync( Guid id, CancellationToken cancellationToken = default )
    {
        var testimonial = await _db.Testimonials
            .FirstOrDefaultAsync( t => t.Id == id, cancellationToken );

        if ( testimonial is null )
            return false;

        _db.Testimonials.Remove( testimonial );
        await _db.SaveChangesAsync( cancellationToken );

        _logger.LogInformation( "Testimonial deleted: {Id}.", id );
        return true;
    }

    // --- Helpers ---

    private static AdminTestimonialDto ToAdminDto( Testimonial t ) =>
        new( t.Id,
             t.FullName,
             t.RoleOrTitle,
             t.CompanyOrContext,
             t.QuoteText,
             t.AvatarPath,
             t.DisplayOrder,
             t.IsActive,
             t.CreatedAt,
             t.UpdatedAt );
}

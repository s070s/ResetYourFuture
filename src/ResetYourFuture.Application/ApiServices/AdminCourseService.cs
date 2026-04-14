using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Shared.DTOs;
using ResetYourFuture.Web.ApiInterfaces;
using ResetYourFuture.Web.Data;
using ResetYourFuture.Web.Domain.Entities;

namespace ResetYourFuture.Web.ApiServices;

/// <summary>
/// Admin CRUD operations for courses.
/// </summary>
public class AdminCourseService(
    IApplicationDbContext db ,
    ILogger<AdminCourseService> logger ) : IAdminCourseService
{
    public async Task<AdminCourseDto?> GetCourseByIdAsync( Guid id )
    {
        var course = await db.Courses
            .AsNoTracking()
            .Include( c => c.Modules )
            .ThenInclude( m => m.Lessons )
            .Include( c => c.Enrollments )
            .FirstOrDefaultAsync( c => c.Id == id );

        return course is null ? null : MapToDto( course );
    }

    public async Task<PagedResult<AdminCourseDto>> GetCoursesAsync( int page , int pageSize , CancellationToken ct = default )
    {
        var totalCount = await db.Courses.CountAsync( ct );

        var items = await db.Courses
            .AsNoTracking()
            .Include( c => c.Modules )
            .ThenInclude( m => m.Lessons )
            .Include( c => c.Enrollments )
            .OrderByDescending( c => c.CreatedAt )
            .Skip( ( page - 1 ) * pageSize )
            .Take( pageSize )
            .Select( c => new AdminCourseDto(
                c.Id ,
                c.TitleEn ,
                c.TitleEl ,
                c.DescriptionEn ,
                c.DescriptionEl ,
                c.IsPublished ,
                c.CreatedAt ,
                c.UpdatedAt ,
                c.Modules.Count ,
                c.Modules.SelectMany( m => m.Lessons ).Count() ,
                c.Enrollments.Count ,
                c.RequiredTier
            ) )
            .ToListAsync( ct );

        return new PagedResult<AdminCourseDto>( items , totalCount , page , pageSize );
    }

    public async Task<AdminCourseDto> CreateCourseAsync( SaveCourseRequest request , string userId )
    {
        var course = new Course
        {
            Id = Guid.NewGuid() ,
            TitleEn = request.TitleEn ,
            TitleEl = request.TitleEl ,
            DescriptionEn = request.DescriptionEn ,
            DescriptionEl = request.DescriptionEl ,
            RequiredTier = request.RequiredTier ,
            IsPublished = false ,
            UpdatedByUserId = userId
        };

        db.Courses.Add( course );
        await db.SaveChangesAsync();

        return new AdminCourseDto(
            course.Id ,
            course.TitleEn ,
            course.TitleEl ,
            course.DescriptionEn ,
            course.DescriptionEl ,
            course.IsPublished ,
            course.CreatedAt ,
            course.UpdatedAt ,
            0 , 0 , 0 ,
            course.RequiredTier
        );
    }

    public async Task<AdminCourseDto?> UpdateCourseAsync( Guid id , SaveCourseRequest request , string userId )
    {
        var course = await db.Courses
            .Include( c => c.Modules )
            .ThenInclude( m => m.Lessons )
            .Include( c => c.Enrollments )
            .FirstOrDefaultAsync( c => c.Id == id );

        if ( course is null )
            return null;

        course.TitleEn = request.TitleEn;
        course.TitleEl = request.TitleEl;
        course.DescriptionEn = request.DescriptionEn;
        course.DescriptionEl = request.DescriptionEl;
        course.RequiredTier = request.RequiredTier;
        course.UpdatedAt = DateTimeOffset.UtcNow;
        course.UpdatedByUserId = userId;

        await db.SaveChangesAsync();

        return MapToDto( course );
    }

    public async Task<bool> DeleteCourseAsync( Guid id , string userId )
    {
        var course = await db.Courses
            .Include( c => c.Enrollments )
            .FirstOrDefaultAsync( c => c.Id == id );

        if ( course is null )
            return false;

        // Soft-delete the course so students retain access to their earned certificates.
        // The global IsDeleted query filter will hide the course and its enrollments
        // from all normal queries while leaving certificate records intact.
        course.IsDeleted = true;
        course.DeletedAt = DateTimeOffset.UtcNow;
        course.UpdatedByUserId = userId;

        await db.SaveChangesAsync();

        logger.LogInformation( "Admin {UserId} soft-deleted course {CourseId} with {Enrollments} enrollment(s)" ,
            userId , id , course.Enrollments.Count );

        return true;
    }

    public async Task<bool> PublishCourseAsync( Guid id , string userId )
    {
        var course = await db.Courses.FindAsync( id );
        if ( course is null )
            return false;

        if ( !course.IsPublished )
        {
            course.IsPublished = true;
            course.PublishedAt = DateTimeOffset.UtcNow;
            course.UpdatedByUserId = userId;
            await db.SaveChangesAsync();
        }

        return true;
    }

    public async Task<bool> UnpublishCourseAsync( Guid id , string userId )
    {
        var course = await db.Courses.FindAsync( id );
        if ( course is null )
            return false;

        if ( course.IsPublished )
        {
            course.IsPublished = false;
            course.UpdatedAt = DateTimeOffset.UtcNow;
            course.UpdatedByUserId = userId;
            await db.SaveChangesAsync();
        }

        return true;
    }

    private static AdminCourseDto MapToDto( Course course ) => new(
        course.Id ,
        course.TitleEn ,
        course.TitleEl ,
        course.DescriptionEn ,
        course.DescriptionEl ,
        course.IsPublished ,
        course.CreatedAt ,
        course.UpdatedAt ,
        course.Modules.Count ,
        course.Modules.SelectMany( m => m.Lessons ).Count() ,
        course.Enrollments.Count ,
        course.RequiredTier
    );
}

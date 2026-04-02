using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.ApiInterfaces;

/// <summary>
/// Admin CRUD operations for courses.
/// </summary>
public interface IAdminCourseService
{
    Task<AdminCourseDto?> GetCourseByIdAsync( Guid id );
    Task<PagedResult<AdminCourseDto>> GetCoursesAsync( int page , int pageSize , CancellationToken ct = default );
    Task<AdminCourseDto> CreateCourseAsync( SaveCourseRequest request , string userId );
    Task<AdminCourseDto?> UpdateCourseAsync( Guid id , SaveCourseRequest request , string userId );
    Task<bool> DeleteCourseAsync( Guid id , string userId );
    Task<bool> PublishCourseAsync( Guid id , string userId );
    Task<bool> UnpublishCourseAsync( Guid id , string userId );
}

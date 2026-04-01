using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.Consumers;

/// <summary>
/// Client consumer for admin course management API operations.
/// </summary>
public interface IAdminCourseConsumer
{
    Task<PagedResult<AdminCourseDto>?> GetCoursesAsync( int page = 1 , int pageSize = 10 );
    Task<AdminCourseDto?> GetCourseAsync( Guid id );
    Task<AdminCourseDto?> CreateCourseAsync( SaveCourseRequest request );
    Task<AdminCourseDto?> UpdateCourseAsync( Guid id , SaveCourseRequest request );
    Task<bool> DeleteCourseAsync( Guid id );
    Task<bool> PublishCourseAsync( Guid id );
    Task<bool> UnpublishCourseAsync( Guid id );
}

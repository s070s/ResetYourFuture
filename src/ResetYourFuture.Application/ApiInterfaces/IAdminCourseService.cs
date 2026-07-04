using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Application.ApiInterfaces;

/// <summary>
/// Admin CRUD operations for courses.
/// </summary>
public interface IAdminCourseService
{
    Task<AdminCourseDto?> GetCourseByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<AdminCourseDto>> GetCoursesAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminCourseDto> CreateCourseAsync(SaveCourseRequest request, string userId, CancellationToken cancellationToken = default);
    Task<AdminCourseDto?> UpdateCourseAsync(Guid id, SaveCourseRequest request, string userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteCourseAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<bool> PublishCourseAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<bool> UnpublishCourseAsync(Guid id, string userId, CancellationToken cancellationToken = default);
}

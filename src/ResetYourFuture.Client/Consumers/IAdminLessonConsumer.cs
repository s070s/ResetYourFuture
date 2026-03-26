using Microsoft.AspNetCore.Components.Forms;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Client.Consumers;

/// <summary>
/// Client consumer for admin lesson management API operations.
/// </summary>
public interface IAdminLessonConsumer
{
    Task<List<AdminLessonDto>> GetLessonsByModuleAsync( Guid moduleId );
    Task<AdminLessonDto?> CreateLessonAsync( SaveLessonRequest request );
    Task<AdminLessonDto?> UpdateLessonAsync( Guid id , SaveLessonRequest request );
    Task<bool> DeleteLessonAsync( Guid id );
    Task<string?> UploadPdfAsync( Guid id , IBrowserFile file );
    Task<string?> UploadVideoAsync( Guid id , IBrowserFile file );
    Task<bool> PublishLessonAsync( Guid id );
}

using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Web.Data;
using ResetYourFuture.Web.Domain.Entities;
using ResetYourFuture.Shared.DTOs;
using System.Text.Json;

namespace ResetYourFuture.Web.ApiServices;

/// <summary>
/// Seeds courses from JSON files located in the Shared project's JSON folder.
/// </summary>
public static class CourseSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Seeds courses from all JSON files in the specified folder.
    /// Only seeds if no courses exist in the database.
    /// </summary>
    public static async Task SeedFromJsonAsync(
        ApplicationDbContext db ,
        string jsonFolderPath ,
        ILogger logger ,
        CancellationToken cancellationToken = default )
    {
        if ( await db.Courses.AnyAsync( cancellationToken ) )
        {
            logger.LogInformation( "Courses already exist; skipping seed." );
            return;
        }

        // Resolve relative paths from the current directory
        var resolvedPath = Path.GetFullPath( jsonFolderPath );

        if ( !Directory.Exists( resolvedPath ) )
        {
            logger.LogWarning( "JSON seed folder not found: {Path}" , resolvedPath );
            return;
        }

        var jsonFiles = Directory.GetFiles( resolvedPath , "*.json" );

        if ( jsonFiles.Length == 0 )
        {
            logger.LogWarning( "No JSON seed files found in: {Path}" , jsonFolderPath );
            return;
        }

        foreach ( var filePath in jsonFiles )
        {
            try
            {
                await SeedCourseFromFileAsync( db , filePath , logger , cancellationToken );
            }
            catch ( Exception ex )
            {
                logger.LogError( ex , "Failed to seed course from file: {FilePath}" , filePath );
            }
        }

        await db.SaveChangesAsync( cancellationToken );
        logger.LogInformation( "Seeded {Count} course(s) from JSON files." , jsonFiles.Length );
    }

    private static async Task SeedCourseFromFileAsync(
        ApplicationDbContext db ,
        string filePath ,
        ILogger logger ,
        CancellationToken cancellationToken )
    {
        var json = await File.ReadAllTextAsync( filePath , cancellationToken );
        var dto = JsonSerializer.Deserialize<CourseSeedDto>( json , JsonOptions );

        if ( dto is null )
        {
            logger.LogWarning( "Failed to deserialize course from: {FilePath}" , filePath );
            return;
        }

        var course = MapToCourse( dto );
        db.Courses.Add( course );

        logger.LogInformation( "Loaded course '{Title}' from {FileName}" ,
            course.TitleEn , Path.GetFileName( filePath ) );
    }

    private static Course MapToCourse( CourseSeedDto dto )
    {
        return new Course
        {
            Id = Guid.NewGuid() ,
            TitleEn = dto.Title ,
            TitleEl = dto.TitleEl ,
            DescriptionEn = dto.Description ,
            DescriptionEl = dto.DescriptionEl ,
            RequiredTier = Enum.TryParse<SubscriptionTierEnum>( dto.RequiredTier , ignoreCase: true , out var tier )
                ? tier
                : SubscriptionTierEnum.Free ,
            IsPublished = dto.IsPublished ,
            Modules = dto.Modules.Select( MapToModule ).ToList()
        };
    }

    private static Module MapToModule( ModuleSeedDto dto )
    {
        return new Module
        {
            Id = Guid.NewGuid() ,
            TitleEn = dto.Title ,
            TitleEl = dto.TitleEl ,
            DescriptionEn = dto.Description ,
            DescriptionEl = dto.DescriptionEl ,
            SortOrder = dto.SortOrder ,
            Lessons = dto.Lessons.Select( MapToLesson ).ToList()
        };
    }

    private static Lesson MapToLesson( LessonSeedDto dto )
    {
        return new Lesson
        {
            Id = Guid.NewGuid() ,
            TitleEn = dto.Title ,
            TitleEl = dto.TitleEl ,
            ContentEn = dto.Content ,
            ContentEl = dto.ContentEl ,
            PdfPath = dto.PdfPath ,
            VideoPath = dto.VideoPath ,
            DurationMinutes = dto.DurationMinutes ,
            SortOrder = dto.SortOrder
        };
    }
}

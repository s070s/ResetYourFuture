using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Api.Data;
using ResetYourFuture.Api.Domain.Entities;
using ResetYourFuture.Shared.DTOs;
using System.Text.Json;

namespace ResetYourFuture.Api.Services;

/// <summary>
/// Seeds assessment definitions from JSON files located in the Shared project's JSON folder.
/// </summary>
public static class AssessmentSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Seeds assessment definitions from all JSON files in the specified folder.
    /// Only seeds if no assessment definitions exist in the database.
    /// </summary>
    public static async Task SeedFromJsonAsync(
        ApplicationDbContext db ,
        string jsonFolderPath ,
        ILogger logger ,
        CancellationToken cancellationToken = default )
    {
        if ( await db.AssessmentDefinitions.AnyAsync( cancellationToken ) )
        {
            logger.LogInformation( "Assessment definitions already exist; skipping seed." );
            return;
        }

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
                await SeedAssessmentFromFileAsync( db , filePath , logger , cancellationToken );
            }
            catch ( Exception ex )
            {
                logger.LogError( ex , "Failed to seed assessment from file: {FilePath}" , filePath );
            }
        }

        await db.SaveChangesAsync( cancellationToken );
        logger.LogInformation( "Seeded {Count} assessment(s) from JSON files." , jsonFiles.Length );
    }

    private static async Task SeedAssessmentFromFileAsync(
        ApplicationDbContext db ,
        string filePath ,
        ILogger logger ,
        CancellationToken cancellationToken )
    {
        var json = await File.ReadAllTextAsync( filePath , cancellationToken );
        var dto = JsonSerializer.Deserialize<AssessmentSeedDto>( json , JsonOptions );

        if ( dto is null )
        {
            logger.LogWarning( "Failed to deserialize assessment from: {FilePath}" , filePath );
            return;
        }

        var def = MapToAssessmentDefinition( dto );
        db.AssessmentDefinitions.Add( def );

        logger.LogInformation( "Loaded assessment '{Title}' (Key: {Key}) from {FileName}" ,
            def.Title , def.Key , Path.GetFileName( filePath ) );
    }

    private static AssessmentDefinition MapToAssessmentDefinition( AssessmentSeedDto dto )
    {
        var now = DateTimeOffset.UtcNow;

        return new AssessmentDefinition
        {
            Id = Guid.NewGuid() ,
            Key = dto.Key ,
            Title = dto.Title ,
            Description = dto.Description ,
            SchemaJson = dto.SchemaJson ,
            CreatedAt = dto.CreatedAt ?? now ,
            UpdatedAt = null ,
            PublishedAt = dto.IsPublished ? ( dto.PublishedAt ?? now ) : null
        };
    }
}
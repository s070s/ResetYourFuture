using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResetYourFuture.Domain.Entities;
using ResetYourFuture.Infrastructure.Data;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

/// <summary>
/// Verifies that the special-category assessment answer/summary columns are encrypted at rest
/// (COMP-2), exercising the real production wiring: the DataProtection provider is injected into
/// <see cref="ApplicationDbContext"/> by DI and its key ring is persisted to the same database.
/// The raw stored value is read back through a second, converter-less context on the same SQLite
/// connection, so it bypasses the value converter and reveals whatever is physically stored.
/// </summary>
[Collection("web-sqlite")]
public class AssessmentEncryptionTests
{
    private readonly SqliteWebAppFactory _factory;

    public AssessmentEncryptionTests(SqliteWebAppFactory factory) => _factory = factory;

    private const string Marker = "SECRET_MARKER_a1b2c3d4";
    private static readonly string Answers = $"{{\"q1\":\"{Marker}\",\"q2\":\"feeling anxious\"}}";
    private static readonly string Summary = $"{{\"insight\":\"{Marker}-summary\"}}";

    private async Task<(string UserId, Guid DefinitionId)> SeedUserAndDefinitionAsync()
    {
        var (_, userId) = await _factory.CreateAuthenticatedClientWithIdAsync("Student");
        var definitionId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.AssessmentDefinitions.Add(new AssessmentDefinition
        {
            Id = definitionId,
            Key = $"enc-test-{Guid.NewGuid():N}",
            TitleEn = "Encryption Test",
            SchemaJson = "{\"questions\":[]}"
        });
        await db.SaveChangesAsync();
        return (userId, definitionId);
    }

    /// <summary>
    /// A converter-less context over the same shared connection: no DataProtection provider means no
    /// encryption converter, so reads/writes hit the columns raw. The encryption-aware model cache
    /// key keeps this model separate from the DI (encrypted) one.
    /// </summary>
    private ApplicationDbContext CreateRawContext()
    {
        using var scope = _factory.Services.CreateScope();
        var connection = (SqliteConnection)scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>().Database.GetDbConnection();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task Submission_AnswersAndSummary_AreCiphertextAtRest_ButDecryptOnRead()
    {
        var (userId, definitionId) = await SeedUserAndDefinitionAsync();
        var submissionId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.AssessmentSubmissions.Add(new AssessmentSubmission
            {
                Id = submissionId,
                AssessmentDefinitionId = definitionId,
                UserId = userId,
                AnswersJson = Answers,
                SummaryJson = Summary,
                SubmittedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // At rest (read through a converter-less context): the stored value is ciphertext.
        await using (var raw = CreateRawContext())
        {
            var stored = await raw.AssessmentSubmissions.AsNoTracking()
                .Where(s => s.Id == submissionId)
                .Select(s => new { s.AnswersJson, s.SummaryJson })
                .SingleAsync();

            stored.AnswersJson.ShouldNotContain(Marker);
            stored.AnswersJson.ShouldNotBe(Answers);
            stored.SummaryJson.ShouldNotBeNull();
            stored.SummaryJson.ShouldNotContain(Marker);
        }

        // On read through the encrypted (DI) context, the converter transparently decrypts.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var loaded = await db.AssessmentSubmissions.AsNoTracking()
                .SingleAsync(s => s.Id == submissionId);
            loaded.AnswersJson.ShouldBe(Answers);
            loaded.SummaryJson.ShouldBe(Summary);
        }
    }

    [Fact]
    public async Task LegacyPlaintextRow_IsReturnedAsIs_NotCrashed()
    {
        // A row written before encryption existed (raw plaintext) must still read back rather than
        // throwing a CryptographicException — the converter tolerates un-protectable values.
        var (userId, definitionId) = await SeedUserAndDefinitionAsync();
        var submissionId = Guid.NewGuid();
        var legacy = $"{{\"legacy\":\"{Marker}\"}}";

        // Write plaintext directly through the converter-less context to simulate a pre-COMP-2 row.
        await using (var raw = CreateRawContext())
        {
            raw.AssessmentSubmissions.Add(new AssessmentSubmission
            {
                Id = submissionId,
                AssessmentDefinitionId = definitionId,
                UserId = userId,
                AnswersJson = legacy,
                SubmittedAt = DateTimeOffset.UtcNow
            });
            await raw.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var loaded = await db.AssessmentSubmissions.AsNoTracking()
                .SingleAsync(s => s.Id == submissionId);
            loaded.AnswersJson.ShouldBe(legacy);
        }
    }
}

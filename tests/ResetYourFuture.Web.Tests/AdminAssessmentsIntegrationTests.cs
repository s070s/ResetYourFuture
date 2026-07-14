using System.Net;
using System.Net.Http.Json;
using ResetYourFuture.Application.DTOs;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

public class AdminAssessmentsIntegrationTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public AdminAssessmentsIntegrationTests(CustomWebAppFactory factory) => _factory = factory;

    private static SaveAssessmentDefinitionRequest Request(string key, string schemaJson) =>
        new(key, "Title", null, null, null, schemaJson);

    [Fact]
    public async Task CreateAssessment_MalformedSchemaJson_Returns400()
    {
        // DQ-4: a non-JSON SchemaJson used to be accepted (only [Required] non-empty was
        // checked) and would only fail later, at student render time.
        var client = await _factory.CreateAuthenticatedClientAsync("Admin");
        var request = Request($"malformed-{Guid.NewGuid():N}", "not json");

        var response = await client.PostAsJsonAsync("/api/admin/assessments", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAssessment_SchemaJsonMissingQuestionsOrSections_Returns400()
    {
        // DQ-4: valid JSON that doesn't match the expected shape (no "questions"/"sections")
        // would previously be stored as-is and silently render nothing for students.
        var client = await _factory.CreateAuthenticatedClientAsync("Admin");
        var request = Request($"wrong-shape-{Guid.NewGuid():N}", "{\"foo\":\"bar\"}");

        var response = await client.PostAsJsonAsync("/api/admin/assessments", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAssessment_ValidSchemaJson_Returns201()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Admin");
        var request = Request($"valid-{Guid.NewGuid():N}", "{\"questions\":[{\"id\":\"q1\",\"type\":\"text\"}]}");

        var response = await client.PostAsJsonAsync("/api/admin/assessments", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateAssessment_DuplicateKey_Returns409()
    {
        // API-3: uniqueness violations are a Conflict ("retry with a different key"), not a
        // BadRequest ("fix your input shape").
        var client = await _factory.CreateAuthenticatedClientAsync("Admin");
        var key = $"dup-{Guid.NewGuid():N}";
        var schemaJson = "{\"questions\":[{\"id\":\"q1\",\"type\":\"text\"}]}";
        (await client.PostAsJsonAsync("/api/admin/assessments", Request(key, schemaJson))).StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await client.PostAsJsonAsync("/api/admin/assessments", Request(key, schemaJson));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}

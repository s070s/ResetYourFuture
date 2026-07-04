using System.Net;
using System.Net.Http.Json;
using ResetYourFuture.Application.DTOs;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

[Collection("web")]
public class TestimonialsIntegrationTests
{
    private readonly CustomWebAppFactory _factory;

    public TestimonialsIntegrationTests(CustomWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task GetActive_Anonymous_Returns200()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/testimonials")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminTestimonials_Student_Returns403()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Student");

        (await client.GetAsync("/api/admin/testimonials")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminTestimonials_Create_Then_GetById_RoundTrips()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Admin");
        var request = new SaveTestimonialRequest("Jane Tester", "QA", "Contoso", "Great platform!", 0, true);

        var created = await client.PostAsJsonAsync("/api/admin/testimonials", request);

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var dto = await created.Content.ReadFromJsonAsync<AdminTestimonialDto>();
        dto!.FullName.ShouldBe("Jane Tester");

        var fetched = await client.GetAsync($"/api/admin/testimonials/{dto.Id}");
        fetched.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminTestimonials_GetById_Unknown_Returns404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("Admin");

        (await client.GetAsync($"/api/admin/testimonials/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}

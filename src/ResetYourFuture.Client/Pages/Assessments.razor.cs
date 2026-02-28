using Microsoft.AspNetCore.Components;
using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Client.Pages;

public partial class Assessments
{
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private List<AssessmentDefinitionDto>? assessments;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            assessments = await Http.GetFromJsonAsync<List<AssessmentDefinitionDto>>( "api/assessments" );
        }
        catch ( Exception ex )
        {
            Console.WriteLine( $"Error loading assessments: {ex.Message}" );
            assessments = new List<AssessmentDefinitionDto>();
        }
    }

    private void StartAssessment( Guid id )
    {
        Nav.NavigateTo( $"/assessments/{id}" );
    }
}

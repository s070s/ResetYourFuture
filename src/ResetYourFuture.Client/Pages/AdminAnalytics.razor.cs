using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace ResetYourFuture.Client.Pages;

public partial class AdminAnalytics
{
    [Inject] private HttpClient Http { get; set; } = default!;

    private AnalyticsStats? stats;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            stats = await Http.GetFromJsonAsync<AnalyticsStats>( "api/admin/analytics/summary" );
        }
        catch ( Exception ex )
        {
            Console.WriteLine( $"Error loading analytics: {ex.Message}" );
        }
    }

    private class AnalyticsStats
    {
        public int TotalUsers
        {
            get; set;
        }
        public int ActiveUsers
        {
            get; set;
        }
        public int TotalEnrollments
        {
            get; set;
        }
        public int CompletedCourses
        {
            get; set;
        }
        public List<CourseStatDto>? CourseStats
        {
            get; set;
        }
        public List<AssessmentStatDto>? AssessmentStats
        {
            get; set;
        }
    }

    private class CourseStatDto
    {
        public string CourseTitle { get; set; } = string.Empty;
        public int EnrollmentCount
        {
            get; set;
        }
        public int CompletionCount
        {
            get; set;
        }
    }

    private class AssessmentStatDto
    {
        public string AssessmentTitle { get; set; } = string.Empty;
        public int SubmissionCount
        {
            get; set;
        }
    }
}

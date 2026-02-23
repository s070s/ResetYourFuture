using Microsoft.AspNetCore.Components;
using ResetYourFuture.Client.Interfaces;
using ResetYourFuture.Shared.Courses;
using System.Text.RegularExpressions;

namespace ResetYourFuture.Client.Pages;

public partial class LessonViewer
{
    [Parameter]
    public Guid LessonId { get; set; }

    [Inject] private ICourseService CourseService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private LessonDetailDto? _lesson;
    private LessonCompletionResultDto? _completionResult;
    private bool _loading = true;
    private bool _completing;
    private string? _error;

    protected override async Task OnParametersSetAsync()
    {
        await LoadLesson();
    }

    private async Task LoadLesson()
    {
        _loading = true;
        _error = null;
        _completionResult = null;

        try
        {
            _lesson = await CourseService.GetLessonAsync(LessonId);
            if (_lesson is null)
            {
                _error = "Lesson not found or you are not enrolled in this course.";
            }
        }
        catch (Exception ex)
        {
            _error = "Failed to load lesson. Please try again.";
            Console.WriteLine(ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task MarkComplete()
    {
        _completing = true;
        try
        {
            _completionResult = await CourseService.CompleteLessonAsync(LessonId);
            if (_completionResult?.Success == true && _lesson is not null)
            {
                // Update local state
                _lesson = _lesson with { IsCompleted = true };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        finally
        {
            _completing = false;
        }
    }

    private void GoToLesson(Guid lessonId)
    {
        Navigation.NavigateTo($"/lessons/{lessonId}");
    }

    private void GoToCourse(Guid courseId)
    {
        Navigation.NavigateTo($"/courses/{courseId}");
    }

    private void GoBack()
    {
        Navigation.NavigateTo("/courses");
    }

    /// <summary>
    /// Detects whether content is already HTML (from WYSIWYG editor) or markdown
    /// (from seed data / plain text input) and renders accordingly.
    /// </summary>
    private static string RenderContent(string content)
    {
        if (string.IsNullOrEmpty(content))
            return "";

        var trimmed = content.TrimStart();
        // If content starts with an HTML tag, treat as pre-rendered HTML from the WYSIWYG editor.
        if (trimmed.StartsWith('<'))
            return content;

        // Otherwise fall back to the simple markdown converter for seed/legacy content.
        return RenderMarkdown(content);
    }

    /// <summary>
    /// Converts YouTube watch/short/youtu.be URLs into embed-friendly URLs.
    /// Returns the original URL unchanged if it's already an embed URL or non-YouTube.
    /// </summary>
    private static string? ToEmbedUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return url;

        // Already an embed URL
        if (url.Contains("/embed/", StringComparison.OrdinalIgnoreCase))
            return url;

        // https://www.youtube.com/watch?v=VIDEO_ID or https://youtube.com/watch?v=VIDEO_ID
        if (url.Contains("youtube.com/watch", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(url);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var videoId = query["v"];
            if (!string.IsNullOrEmpty(videoId))
                return $"https://www.youtube.com/embed/{videoId}";
        }

        // https://youtu.be/VIDEO_ID
        if (url.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(url);
            var videoId = uri.AbsolutePath.TrimStart('/');
            if (!string.IsNullOrEmpty(videoId))
                return $"https://www.youtube.com/embed/{videoId}";
        }

        // https://www.youtube.com/shorts/VIDEO_ID
        if (url.Contains("/shorts/", StringComparison.OrdinalIgnoreCase))
        {
            var idx = url.IndexOf("/shorts/", StringComparison.OrdinalIgnoreCase);
            var videoId = url[(idx + 8)..].Split('?')[0].Split('/')[0];
            if (!string.IsNullOrEmpty(videoId))
                return $"https://www.youtube.com/embed/{videoId}";
        }

        return url;
    }

    /// <summary>
    /// Simple markdown to HTML converter for basic formatting.
    /// Handles headers, bold, lists. For production, use a proper library.
    /// </summary>
    private static string RenderMarkdown(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return "";

        var lines = markdown.Split('\n');
        var html = new System.Text.StringBuilder();
        var inList = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Headers
            if (trimmed.StartsWith("### "))
            {
                CloseList(html, ref inList);
                html.Append($"<h3>{trimmed[4..]}</h3>");
            }
            else if (trimmed.StartsWith("## "))
            {
                CloseList(html, ref inList);
                html.Append($"<h2>{trimmed[3..]}</h2>");
            }
            else if (trimmed.StartsWith("# "))
            {
                CloseList(html, ref inList);
                html.Append($"<h1>{trimmed[2..]}</h1>");
            }
            // Unordered list
            else if (trimmed.StartsWith("- "))
            {
                if (!inList)
                {
                    html.Append("<ul>");
                    inList = true;
                }
                html.Append($"<li>{ProcessInline(trimmed[2..])}</li>");
            }
            // Numbered list
            else if (trimmed.Length > 2 && char.IsDigit(trimmed[0]) && trimmed[1] == '.')
            {
                if (!inList)
                {
                    html.Append("<ol>");
                    inList = true;
                }
                html.Append($"<li>{ProcessInline(trimmed[2..].TrimStart())}</li>");
            }
            // Empty line
            else if (string.IsNullOrEmpty(trimmed))
            {
                CloseList(html, ref inList);
                html.Append("<br/>");
            }
            // Paragraph
            else
            {
                CloseList(html, ref inList);
                html.Append($"<p>{ProcessInline(trimmed)}</p>");
            }
        }

        CloseList(html, ref inList);
        return html.ToString();
    }

    private static void CloseList(System.Text.StringBuilder html, ref bool inList)
    {
        if (inList)
        {
            html.Append("</ul>");
            inList = false;
        }
    }

    private static string ProcessInline(string text)
    {
        // Bold: **text**
        text = Regex.Replace(
            text, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        return text;
    }
}

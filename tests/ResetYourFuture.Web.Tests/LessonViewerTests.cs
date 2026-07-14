using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Web.Pages;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

/// <summary>
/// bUnit component tests for LessonViewer (TEST-4) — the completion flow, load/error
/// states, and the private markdown/YouTube-embed rendering logic that no other test in
/// the suite exercises.
/// </summary>
public class LessonViewerTests : BunitContext
{
    private readonly ICourseConsumer _courseService = Substitute.For<ICourseConsumer>();
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly Guid _lessonId = Guid.NewGuid();

    public LessonViewerTests()
    {
        Services.AddSingleton(_courseService);
        Services.AddSingleton(_authService);

        var authStateProvider = Substitute.For<AuthenticationStateProvider>();
        authStateProvider.GetAuthenticationStateAsync().Returns(
            new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
        Services.AddSingleton(authStateProvider);
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        _authService.GetTokenAsync(Arg.Any<ClaimsPrincipal>()).Returns((string?)null);
    }

    private static LessonDetailDto Lesson(
        int contentType = 1,
        string? content = "Hello",
        bool isCompleted = false,
        Guid? previousLessonId = null,
        Guid? nextLessonId = null) => new(
        Guid.NewGuid(),
        "Intro to Testing",
        contentType,
        content,
        PdfPath: null,
        DurationMinutes: 10,
        isCompleted,
        Guid.NewGuid(),
        "Module 1",
        Guid.NewGuid(),
        "Course 1",
        previousLessonId,
        nextLessonId);

    [Fact]
    public void Loading_ShowsSpinnerBeforeLessonResolves()
    {
        var tcs = new TaskCompletionSource<LessonDetailDto?>();
        _courseService.GetLessonAsync(_lessonId, Arg.Any<string>()).Returns(tcs.Task);

        var cut = Render<LessonViewer>(p => p.Add(c => c.LessonId, _lessonId));

        cut.Find(".compass-spinner").ShouldNotBeNull();
    }

    [Fact]
    public void LessonMissing_ShowsNotFoundError()
    {
        _courseService.GetLessonAsync(_lessonId, Arg.Any<string>()).Returns((LessonDetailDto?)null);

        var cut = Render<LessonViewer>(p => p.Add(c => c.LessonId, _lessonId));

        cut.Find(".text-danger").TextContent.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void LoadThrows_ShowsFailedToLoadError()
    {
        _courseService.GetLessonAsync(_lessonId, Arg.Any<string>())
            .Returns(Task.FromException<LessonDetailDto?>(new HttpRequestException("boom")));

        var cut = Render<LessonViewer>(p => p.Add(c => c.LessonId, _lessonId));

        cut.Find(".text-danger").ShouldNotBeNull();
    }

    [Fact]
    public void LessonLoaded_RendersTitleAndBreadcrumb()
    {
        var lesson = Lesson();
        _courseService.GetLessonAsync(_lessonId, Arg.Any<string>()).Returns(lesson);

        var cut = Render<LessonViewer>(p => p.Add(c => c.LessonId, _lessonId));

        cut.Find("h1").TextContent.ShouldBe(lesson.Title);
        cut.Find(".module-name").TextContent.ShouldBe(lesson.ModuleTitle);
    }

    [Fact]
    public void MarkComplete_Success_ReplacesButtonWithCompletedIndicator()
    {
        var lesson = Lesson(isCompleted: false);
        _courseService.GetLessonAsync(_lessonId, Arg.Any<string>()).Returns(lesson);
        _courseService.CompleteLessonAsync(_lessonId).Returns(
            new LessonCompletionResultDto(true, "ok", CompletedLessons: 2, TotalLessons: 5, ProgressPercent: 40, CourseCompleted: false));

        var cut = Render<LessonViewer>(p => p.Add(c => c.LessonId, _lessonId));
        cut.Find(".btn-success").Click();

        cut.Find(".completed-indicator").ShouldNotBeNull();
        cut.Find(".progress-update").TextContent.ShouldContain("2");
    }

    [Fact]
    public void MarkComplete_Failure_ShowsCompletionError()
    {
        var lesson = Lesson(isCompleted: false);
        _courseService.GetLessonAsync(_lessonId, Arg.Any<string>()).Returns(lesson);
        _courseService.CompleteLessonAsync(_lessonId).Returns((LessonCompletionResultDto?)null);

        var cut = Render<LessonViewer>(p => p.Add(c => c.LessonId, _lessonId));
        cut.Find(".btn-success").Click();

        cut.Find(".lesson-actions .text-danger").ShouldNotBeNull();
    }

    [Fact]
    public void AlreadyCompleted_ShowsIndicatorNotButton()
    {
        var lesson = Lesson(isCompleted: true);
        _courseService.GetLessonAsync(_lessonId, Arg.Any<string>()).Returns(lesson);

        var cut = Render<LessonViewer>(p => p.Add(c => c.LessonId, _lessonId));

        cut.Find(".completed-indicator").ShouldNotBeNull();
        cut.FindAll(".btn-success").ShouldBeEmpty();
    }

    [Fact]
    public void VideoLesson_YouTubeUrl_RendersEmbedIframe()
    {
        var lesson = Lesson(contentType: 2, content: "https://www.youtube.com/watch?v=abc123XYZ");
        _courseService.GetLessonAsync(_lessonId, Arg.Any<string>()).Returns(lesson);

        var cut = Render<LessonViewer>(p => p.Add(c => c.LessonId, _lessonId));

        var iframe = cut.Find(".video-container iframe");
        iframe.GetAttribute("src").ShouldBe("https://www.youtube.com/embed/abc123XYZ");
    }

    [Fact]
    public void VideoLesson_UploadedFile_RendersVideoElementNotIframe()
    {
        var lesson = Lesson(contentType: 2, content: "/media/lesson-video.mp4");
        _courseService.GetLessonAsync(_lessonId, Arg.Any<string>()).Returns(lesson);

        var cut = Render<LessonViewer>(p => p.Add(c => c.LessonId, _lessonId));

        cut.FindAll(".video-container iframe").ShouldBeEmpty();
        cut.Find(".video-container video").ShouldNotBeNull();
    }

    [Fact]
    public void TextLesson_MarkdownContent_RendersHeadingAndListItems()
    {
        var lesson = Lesson(contentType: 1, content: "# Heading\n- one\n- two");
        _courseService.GetLessonAsync(_lessonId, Arg.Any<string>()).Returns(lesson);

        var cut = Render<LessonViewer>(p => p.Add(c => c.LessonId, _lessonId));

        var content = cut.Find(".text-content");
        content.InnerHtml.ShouldContain("<h1>Heading</h1>");
        content.QuerySelectorAll("li").Length.ShouldBe(2);
    }

    [Fact]
    public void TextLesson_AlreadyHtmlContent_PassesThroughUnchanged()
    {
        var lesson = Lesson(contentType: 1, content: "<p><strong>bold</strong> text</p>");
        _courseService.GetLessonAsync(_lessonId, Arg.Any<string>()).Returns(lesson);

        var cut = Render<LessonViewer>(p => p.Add(c => c.LessonId, _lessonId));

        cut.Find(".text-content").InnerHtml.ShouldContain("<strong>bold</strong>");
    }

    [Fact]
    public void NoNextLesson_ShowsBackToCourseButton()
    {
        var lesson = Lesson(nextLessonId: null);
        _courseService.GetLessonAsync(_lessonId, Arg.Any<string>()).Returns(lesson);

        var cut = Render<LessonViewer>(p => p.Add(c => c.LessonId, _lessonId));

        cut.Find(".lesson-navigation .btn-primary, .lesson-navigation .btn-secondary:last-child")
            .ShouldNotBeNull();
        cut.FindAll(".lesson-navigation .btn-primary").ShouldBeEmpty();
    }

    [Fact]
    public void HasNextLesson_ShowsNextLessonButton()
    {
        var lesson = Lesson(nextLessonId: Guid.NewGuid());
        _courseService.GetLessonAsync(_lessonId, Arg.Any<string>()).Returns(lesson);

        var cut = Render<LessonViewer>(p => p.Add(c => c.LessonId, _lessonId));

        cut.Find(".lesson-navigation .btn-primary").ShouldNotBeNull();
    }
}

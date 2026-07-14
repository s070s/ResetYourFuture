namespace ResetYourFuture.Application.DTOs;

/// <summary>
/// Payload of the short-lived signed DataProtection token used to authorize browser-rendered
/// &lt;video&gt;/&lt;iframe&gt; requests to <c>/api/lessons/{lessonId}/asset</c> (SEC-2).
/// Scoped to one user and one lesson — unlike the general-purpose access JWT it replaced here,
/// leaking this token grants nothing beyond that single asset for its ~10-minute lifetime.
/// </summary>
public record LessonAssetTicket(string UserId, Guid LessonId);

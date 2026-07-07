namespace ResetYourFuture.Domain.Enums;

/// <summary>
/// The kind of published content an <see cref="Entities.AssistantContentChunk"/> was derived from.
/// Stored as int.
/// </summary>
public enum AssistantSourceType
{
    Course = 1,
    Lesson = 2,
    Assessment = 3,
    BlogArticle = 4
}

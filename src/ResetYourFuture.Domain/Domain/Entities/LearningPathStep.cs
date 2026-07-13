namespace ResetYourFuture.Domain.Entities;

/// <summary>
/// One course in a LearningPath's ordered sequence.
/// </summary>
public class LearningPathStep
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LearningPathId { get; set; }

    public LearningPath? LearningPath { get; set; }

    public Guid CourseId { get; set; }

    public Course? Course { get; set; }

    /// <summary>1-based position within the path. Unique per (LearningPathId, StepOrder).</summary>
    public int StepOrder { get; set; }
}

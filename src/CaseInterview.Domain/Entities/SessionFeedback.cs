using CaseInterview.Domain.ValueObjects;

namespace CaseInterview.Domain.Entities;

public class SessionFeedback
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public InterviewSession Session { get; set; } = null!;

    public double OverallScore { get; set; }
    public List<RubricScore> RubricScores { get; set; } = new();
    public List<string> Strengths { get; set; } = new();
    public List<string> Gaps { get; set; } = new();
    public List<ChapterLink> ChapterLinks { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

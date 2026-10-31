using CaseInterview.Domain.Enums;

namespace CaseInterview.Domain.Entities;

public class InterviewSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid CaseId { get; set; }
    public InterviewCase Case { get; set; } = null!;

    public SessionStage Stage { get; set; } = SessionStage.Clarification;
    public SessionStatus Status { get; set; } = SessionStatus.InProgress;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public double? FinalScore { get; set; }

    public ICollection<SessionMessage> Messages { get; set; } = new List<SessionMessage>();
    public SessionFeedback? Feedback { get; set; }
}

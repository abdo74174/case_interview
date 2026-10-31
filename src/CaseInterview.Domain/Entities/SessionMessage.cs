using CaseInterview.Domain.Enums;

namespace CaseInterview.Domain.Entities;

public class SessionMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public InterviewSession Session { get; set; } = null!;

    public MessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public SessionStage Stage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

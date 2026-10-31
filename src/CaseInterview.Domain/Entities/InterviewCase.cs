using CaseInterview.Domain.ValueObjects;

namespace CaseInterview.Domain.Entities;

public class InterviewCase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChapterId { get; set; }
    public Chapter Chapter { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Scenario { get; set; } = string.Empty;

    public List<string> HiddenRequirements { get; set; } = new();
    public Dictionary<string, string> Numbers { get; set; } = new();
    public List<string> Twists { get; set; } = new();
    public List<RubricItem> Rubric { get; set; } = new();

    public string Difficulty { get; set; } = "Mid-Level";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<InterviewSession> Sessions { get; set; } = new List<InterviewSession>();
}

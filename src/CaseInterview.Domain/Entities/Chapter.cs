namespace CaseInterview.Domain.Entities;

public class Chapter
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;

    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ChunkEmbedding> Chunks { get; set; } = new List<ChunkEmbedding>();
    public ICollection<InterviewCase> Cases { get; set; } = new List<InterviewCase>();
}

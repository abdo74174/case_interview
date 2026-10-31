using Pgvector;

namespace CaseInterview.Domain.Entities;

public class ChunkEmbedding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChapterId { get; set; }
    public Chapter Chapter { get; set; } = null!;

    public string Text { get; set; } = string.Empty;
    public Vector Embedding { get; set; } = null!;
    public int TokenCount { get; set; }
    public int ChunkIndex { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

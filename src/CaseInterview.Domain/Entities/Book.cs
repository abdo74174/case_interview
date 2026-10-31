using CaseInterview.Domain.Enums;

namespace CaseInterview.Domain.Entities;

public class Book
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }

    public BookStatus Status { get; set; } = BookStatus.Uploaded;
    public string? FailureReason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();
}

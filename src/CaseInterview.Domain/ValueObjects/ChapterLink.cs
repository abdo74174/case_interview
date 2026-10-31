namespace CaseInterview.Domain.ValueObjects;

public sealed record ChapterLink(
    Guid ChapterId,
    string ChapterTitle,
    string Reason
);

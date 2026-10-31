namespace CaseInterview.Application.Common.Models;

public sealed record ExtractedChapter(
    int Order,
    string Title,
    string RawText
);

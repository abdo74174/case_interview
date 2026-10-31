using CaseInterview.Domain.Enums;

namespace CaseInterview.Application.Features.Books.DTOs;

public sealed record BookDto(
    Guid Id,
    string Title,
    string? Author,
    BookStatus Status,
    string? FailureReason,
    long FileSizeBytes,
    int ChapterCount,
    DateTime CreatedAtUtc
);

public sealed record ChapterSummaryDto(
    Guid Id,
    int Order,
    string Title,
    int ChunkCount,
    int CharacterCount
);

public sealed record BookDetailDto(
    Guid Id,
    string Title,
    string? Author,
    BookStatus Status,
    string? FailureReason,
    long FileSizeBytes,
    DateTime CreatedAtUtc,
    List<ChapterSummaryDto> Chapters
);

public sealed record ChapterDetailDto(
    Guid Id,
    Guid BookId,
    int Order,
    string Title,
    string RawText,
    int ChunkCount,
    DateTime CreatedAtUtc
);

public sealed record MergeChaptersRequest(
    List<Guid> ChapterIds,
    string NewTitle
);

public sealed record SplitChapterRequest(
    string SplitMarker, // Text substring or regex pattern where split occurs
    string FirstChapterTitle,
    string SecondChapterTitle
);

public sealed record UpdateChapterRequest(
    string? Title,
    string? RawText
);

public sealed record ReorderChapterItem(
    Guid ChapterId,
    int NewOrder
);

public sealed record ReorderChaptersRequest(
    List<ReorderChapterItem> Chapters
);

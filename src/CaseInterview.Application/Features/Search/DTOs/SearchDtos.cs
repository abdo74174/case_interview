namespace CaseInterview.Application.Features.Search.DTOs;

public sealed record BookSearchRequest(
    string Query,
    int TopK = 5
);

public sealed record ChunkResultDto(
    Guid ChunkId,
    Guid ChapterId,
    string ChapterTitle,
    int ChapterOrder,
    string Text,
    int TokenCount,
    double Distance,
    double Similarity
);

public sealed record BookSearchResultDto(
    Guid BookId,
    string Query,
    int TotalReturned,
    List<ChunkResultDto> Results
);

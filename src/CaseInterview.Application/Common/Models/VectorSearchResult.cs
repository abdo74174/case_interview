namespace CaseInterview.Application.Common.Models;

public sealed record VectorSearchResult(
    Guid ChunkId,
    Guid ChapterId,
    string ChapterTitle,
    int ChapterOrder,
    string Text,
    int TokenCount,
    double Distance,
    double Similarity
);

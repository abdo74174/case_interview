using CaseInterview.Application.Common.Interfaces;
using CaseInterview.Application.Features.Search.DTOs;
using CaseInterview.Domain.Entities;
using CaseInterview.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace CaseInterview.Application.Features.Search.Services;

public interface ISearchService
{
    Task<BookSearchResultDto> SearchBookChunksAsync(Guid bookId, BookSearchRequest request, CancellationToken cancellationToken = default);
}

public class SearchService : ISearchService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmbeddingService _embeddingService;

    public SearchService(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IEmbeddingService embeddingService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _embeddingService = embeddingService;
    }

    public async Task<BookSearchResultDto> SearchBookChunksAsync(Guid bookId, BookSearchRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedException("User is not authenticated.");

        var book = await _dbContext.Books
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bookId && b.OwnerId == userId, cancellationToken);

        if (book == null)
            throw new NotFoundException(nameof(Book), bookId);

        // Generate embedding for user query
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(request.Query, cancellationToken);
        var queryVector = new Vector(queryEmbedding.ToArray());

        var topK = Math.Clamp(request.TopK, 1, 50);

        List<ChunkResultDto> dtos;

        if (_dbContext.Database.IsNpgsql())
        {
            // Native pgvector cosine distance search
            var chunkResults = await _dbContext.ChunkEmbeddings
                .AsNoTracking()
                .Where(c => c.Chapter.BookId == bookId)
                .OrderBy(c => c.Embedding.CosineDistance(queryVector))
                .Take(topK)
                .Select(c => new
                {
                    c.Id,
                    c.ChapterId,
                    ChapterTitle = c.Chapter.Title,
                    ChapterOrder = c.Chapter.Order,
                    c.Text,
                    c.TokenCount,
                    Distance = c.Embedding.CosineDistance(queryVector)
                })
                .ToListAsync(cancellationToken);

            dtos = chunkResults.Select(c =>
            {
                var similarity = Math.Clamp((2.0 - c.Distance) / 2.0, 0.0, 1.0);
                return new ChunkResultDto(
                    ChunkId: c.Id,
                    ChapterId: c.ChapterId,
                    ChapterTitle: c.ChapterTitle,
                    ChapterOrder: c.ChapterOrder,
                    Text: c.Text,
                    TokenCount: c.TokenCount,
                    Distance: Math.Round(c.Distance, 5),
                    Similarity: Math.Round(similarity, 5)
                );
            }).ToList();
        }
        else
        {
            // In-memory fallback computation of cosine distance for test environments
            var allChunks = await _dbContext.ChunkEmbeddings
                .AsNoTracking()
                .Where(c => c.Chapter.BookId == bookId)
                .Select(c => new
                {
                    c.Id,
                    c.ChapterId,
                    ChapterTitle = c.Chapter.Title,
                    ChapterOrder = c.Chapter.Order,
                    c.Text,
                    c.TokenCount,
                    c.Embedding
                })
                .ToListAsync(cancellationToken);

            dtos = allChunks.Select(c =>
            {
                var distance = ComputeCosineDistance(c.Embedding, queryVector);
                var similarity = Math.Clamp((2.0 - distance) / 2.0, 0.0, 1.0);
                return new
                {
                    Dto = new ChunkResultDto(
                        ChunkId: c.Id,
                        ChapterId: c.ChapterId,
                        ChapterTitle: c.ChapterTitle,
                        ChapterOrder: c.ChapterOrder,
                        Text: c.Text,
                        TokenCount: c.TokenCount,
                        Distance: Math.Round(distance, 5),
                        Similarity: Math.Round(similarity, 5)
                    ),
                    Distance = distance
                };
            })
            .OrderBy(x => x.Distance)
            .Take(topK)
            .Select(x => x.Dto)
            .ToList();
        }

        return new BookSearchResultDto(
            BookId: bookId,
            Query: request.Query,
            TotalReturned: dtos.Count,
            Results: dtos
        );
    }

    private static double ComputeCosineDistance(Vector a, Vector b)
    {
        if (a == null || b == null) return 1.0;
        var aArr = a.ToArray();
        var bArr = b.ToArray();

        double dot = 0;
        double normA = 0;
        double normB = 0;

        for (var i = 0; i < aArr.Length && i < bArr.Length; i++)
        {
            dot += aArr[i] * bArr[i];
            normA += aArr[i] * aArr[i];
            normB += bArr[i] * bArr[i];
        }

        if (normA <= 0 || normB <= 0) return 1.0;

        var cosineSimilarity = dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
        return Math.Clamp(1.0 - cosineSimilarity, 0.0, 2.0);
    }
}

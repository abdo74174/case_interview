using CaseInterview.Application.Common.Interfaces;
using CaseInterview.Domain.Entities;
using CaseInterview.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pgvector;

namespace CaseInterview.Infrastructure.BackgroundProcessing;

public class BookIngestionBackgroundService : BackgroundService
{
    private readonly IIngestionQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookIngestionBackgroundService> _logger;

    public BookIngestionBackgroundService(
        IIngestionQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<BookIngestionBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Book Ingestion Background Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var task = await _queue.DequeueAsync(stoppingToken);
                _logger.LogInformation("Dequeued ingestion task for Book {BookId}, Step {Step}", task.BookId, task.Step);

                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

                switch (task.Step)
                {
                    case IngestionStep.ExtractChapters:
                        await ProcessChapterExtractionAsync(task.BookId, scope.ServiceProvider, stoppingToken);
                        break;

                    case IngestionStep.GenerateEmbeddings:
                        await ProcessEmbeddingGenerationAsync(task.BookId, scope.ServiceProvider, stoppingToken);
                        break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in Book Ingestion Background Service loop.");
            }
        }

        _logger.LogInformation("Book Ingestion Background Service stopped.");
    }

    private async Task ProcessChapterExtractionAsync(Guid bookId, IServiceProvider sp, CancellationToken cancellationToken)
    {
        var dbContext = sp.GetRequiredService<IApplicationDbContext>();
        var extractor = sp.GetRequiredService<IPdfExtractor>();
        var fileStorage = sp.GetRequiredService<IFileStorageService>();

        var book = await dbContext.Books
            .Include(b => b.Chapters)
            .FirstOrDefaultAsync(b => b.Id == bookId, cancellationToken);

        if (book == null)
        {
            _logger.LogWarning("Book {BookId} not found for extraction.", bookId);
            return;
        }

        try
        {
            book.Status = BookStatus.Extracting;
            book.FailureReason = null;
            book.UpdatedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            await using var stream = fileStorage.OpenReadStream(book.FilePath);
            var extractedChapters = await extractor.ExtractChaptersAsync(stream, cancellationToken);

            if (extractedChapters.Count == 0)
            {
                throw new InvalidOperationException("No readable text or chapters could be extracted from this PDF.");
            }

            // Remove existing chapters if re-extracting
            if (book.Chapters.Count > 0)
            {
                dbContext.Chapters.RemoveRange(book.Chapters);
            }

            var order = 1;
            foreach (var chapter in extractedChapters)
            {
                var newChapter = new Chapter
                {
                    BookId = book.Id,
                    Order = order++,
                    Title = string.IsNullOrWhiteSpace(chapter.Title) ? $"Chapter {order - 1}" : chapter.Title.Trim(),
                    RawText = chapter.RawText,
                    CreatedAtUtc = DateTime.UtcNow
                };

                dbContext.Chapters.Add(newChapter);
            }

            book.Status = BookStatus.NeedsChapterReview;
            book.UpdatedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully extracted {Count} chapters for Book {BookId}. Awaiting user review.",
                extractedChapters.Count, book.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract chapters for Book {BookId}", bookId);
            book.Status = BookStatus.Failed;
            book.FailureReason = ex.Message;
            book.UpdatedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ProcessEmbeddingGenerationAsync(Guid bookId, IServiceProvider sp, CancellationToken cancellationToken)
    {
        var dbContext = sp.GetRequiredService<IApplicationDbContext>();
        var chunker = sp.GetRequiredService<ITextChunker>();
        var embeddingService = sp.GetRequiredService<IEmbeddingService>();

        var book = await dbContext.Books
            .Include(b => b.Chapters)
            .FirstOrDefaultAsync(b => b.Id == bookId, cancellationToken);

        if (book == null)
        {
            _logger.LogWarning("Book {BookId} not found for embedding generation.", bookId);
            return;
        }

        try
        {
            book.Status = BookStatus.Embedding;
            book.FailureReason = null;
            book.UpdatedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            var chapters = book.Chapters.OrderBy(c => c.Order).ToList();
            if (chapters.Count == 0)
            {
                throw new InvalidOperationException("Book has no chapters to embed.");
            }

            foreach (var chapter in chapters)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Delete existing chunks for this chapter
                var existingChunks = await dbContext.ChunkEmbeddings
                    .Where(c => c.ChapterId == chapter.Id)
                    .ToListAsync(cancellationToken);

                if (existingChunks.Count > 0)
                {
                    dbContext.ChunkEmbeddings.RemoveRange(existingChunks);
                }

                // Chunk chapter text: 300-500 tokens, 50 token overlap
                var chunks = chunker.ChunkText(chapter.RawText, minTokens: 300, maxTokens: 500, overlapTokens: 50);

                if (chunks.Count == 0)
                    continue;

                var textsToEmbed = chunks.Select(c => c.Text).ToList();
                var embeddings = await embeddingService.GenerateEmbeddingsBatchAsync(textsToEmbed, cancellationToken);

                for (var i = 0; i < chunks.Count; i++)
                {
                    var chunk = chunks[i];
                    var embeddingMemory = embeddings[i];

                    var chunkEmbedding = new ChunkEmbedding
                    {
                        ChapterId = chapter.Id,
                        Text = chunk.Text,
                        TokenCount = chunk.TokenCount,
                        ChunkIndex = chunk.Index,
                        Embedding = new Vector(embeddingMemory.ToArray()),
                        CreatedAtUtc = DateTime.UtcNow
                    };

                    dbContext.ChunkEmbeddings.Add(chunkEmbedding);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Embedded {ChunkCount} chunks for Chapter '{Title}' (Id: {ChapterId})",
                    chunks.Count, chapter.Title, chapter.Id);
            }

            book.Status = BookStatus.Ready;
            book.UpdatedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Book {BookId} is now READY with full embeddings.", book.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embeddings for Book {BookId}", bookId);
            book.Status = BookStatus.Failed;
            book.FailureReason = ex.Message;
            book.UpdatedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

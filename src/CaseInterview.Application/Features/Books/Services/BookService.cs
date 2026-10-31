using System.Text.RegularExpressions;
using CaseInterview.Application.Common.Interfaces;
using CaseInterview.Application.Features.Books.DTOs;
using CaseInterview.Domain.Entities;
using CaseInterview.Domain.Enums;
using CaseInterview.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CaseInterview.Application.Features.Books.Services;

public interface IBookService
{
    Task<BookDto> UploadBookAsync(Stream fileStream, string fileName, long fileSizeBytes, string? title, string? author, CancellationToken cancellationToken = default);
    Task<List<BookDto>> ListBooksAsync(CancellationToken cancellationToken = default);
    Task<BookDetailDto> GetBookAsync(Guid bookId, CancellationToken cancellationToken = default);
    Task DeleteBookAsync(Guid bookId, CancellationToken cancellationToken = default);

    Task<List<ChapterDetailDto>> GetChaptersAsync(Guid bookId, CancellationToken cancellationToken = default);
    Task<ChapterDetailDto> GetChapterAsync(Guid chapterId, CancellationToken cancellationToken = default);
    Task<ChapterDetailDto> UpdateChapterAsync(Guid chapterId, UpdateChapterRequest request, CancellationToken cancellationToken = default);
    Task<ChapterDetailDto> MergeChaptersAsync(Guid bookId, MergeChaptersRequest request, CancellationToken cancellationToken = default);
    Task<List<ChapterDetailDto>> SplitChapterAsync(Guid chapterId, SplitChapterRequest request, CancellationToken cancellationToken = default);
    Task<List<ChapterDetailDto>> ReorderChaptersAsync(Guid bookId, ReorderChaptersRequest request, CancellationToken cancellationToken = default);
    Task ConfirmChaptersAsync(Guid bookId, CancellationToken cancellationToken = default);
}

public class BookService : IBookService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IIngestionQueue _ingestionQueue;

    public BookService(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IFileStorageService fileStorageService,
        IIngestionQueue ingestionQueue)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _fileStorageService = fileStorageService;
        _ingestionQueue = ingestionQueue;
    }

    private Guid CurrentUserId =>
        _currentUserService.UserId ?? throw new UnauthorizedException("User is not authenticated.");

    public async Task<BookDto> UploadBookAsync(Stream fileStream, string fileName, long fileSizeBytes, string? title, string? author, CancellationToken cancellationToken = default)
    {
        var savedPath = await _fileStorageService.SaveFileAsync(fileStream, fileName, cancellationToken);
        var finalTitle = !string.IsNullOrWhiteSpace(title)
            ? title.Trim()
            : Path.GetFileNameWithoutExtension(fileName).Replace('_', ' ');

        var book = new Book
        {
            OwnerId = CurrentUserId,
            Title = finalTitle,
            Author = author?.Trim(),
            FilePath = savedPath,
            FileSizeBytes = fileSizeBytes,
            Status = BookStatus.Uploaded,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Books.Add(book);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Enqueue background extraction task
        await _ingestionQueue.EnqueueAsync(new IngestionTask(book.Id, IngestionStep.ExtractChapters), cancellationToken);

        return new BookDto(
            Id: book.Id,
            Title: book.Title,
            Author: book.Author,
            Status: book.Status,
            FailureReason: book.FailureReason,
            FileSizeBytes: book.FileSizeBytes,
            ChapterCount: 0,
            CreatedAtUtc: book.CreatedAtUtc
        );
    }

    public async Task<List<BookDto>> ListBooksAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        var isAdmin = _currentUserService.IsAdmin;

        var query = _dbContext.Books.AsNoTracking();
        if (userId.HasValue && !isAdmin)
        {
            query = query.Where(b => b.OwnerId == userId.Value);
        }

        return await query
            .OrderByDescending(b => b.CreatedAtUtc)
            .Select(b => new BookDto(
                b.Id,
                b.Title,
                b.Author,
                b.Status,
                b.FailureReason,
                b.FileSizeBytes,
                b.Chapters.Count,
                b.CreatedAtUtc
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<BookDetailDto> GetBookAsync(Guid bookId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        var isAdmin = _currentUserService.IsAdmin;

        var book = await _dbContext.Books
            .AsNoTracking()
            .Include(b => b.Chapters)
                .ThenInclude(c => c.Chunks)
            .FirstOrDefaultAsync(b => b.Id == bookId && (!userId.HasValue || isAdmin || b.OwnerId == userId.Value), cancellationToken);

        if (book == null)
            throw new NotFoundException(nameof(Book), bookId);

        var chapters = book.Chapters
            .OrderBy(c => c.Order)
            .Select(c => new ChapterSummaryDto(
                c.Id,
                c.Order,
                c.Title,
                c.Chunks.Count,
                c.RawText.Length
            ))
            .ToList();

        return new BookDetailDto(
            book.Id,
            book.Title,
            book.Author,
            book.Status,
            book.FailureReason,
            book.FileSizeBytes,
            book.CreatedAtUtc,
            chapters
        );
    }

    public async Task DeleteBookAsync(Guid bookId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;
        var isAdmin = _currentUserService.IsAdmin;

        var book = await _dbContext.Books
            .FirstOrDefaultAsync(b => b.Id == bookId && (isAdmin || b.OwnerId == userId), cancellationToken);

        if (book == null)
            throw new NotFoundException(nameof(Book), bookId);

        await _fileStorageService.DeleteFileAsync(book.FilePath, cancellationToken);

        _dbContext.Books.Remove(book);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ChapterDetailDto>> GetChaptersAsync(Guid bookId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        var isAdmin = _currentUserService.IsAdmin;

        var bookExists = await _dbContext.Books
            .AnyAsync(b => b.Id == bookId && (!userId.HasValue || isAdmin || b.OwnerId == userId.Value), cancellationToken);

        if (!bookExists)
            throw new NotFoundException(nameof(Book), bookId);

        return await _dbContext.Chapters
            .AsNoTracking()
            .Where(c => c.BookId == bookId)
            .OrderBy(c => c.Order)
            .Select(c => new ChapterDetailDto(
                c.Id,
                c.BookId,
                c.Order,
                c.Title,
                c.RawText,
                c.Chunks.Count,
                c.CreatedAtUtc
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<ChapterDetailDto> GetChapterAsync(Guid chapterId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;

        var chapter = await _dbContext.Chapters
            .AsNoTracking()
            .Include(c => c.Book)
            .Include(c => c.Chunks)
            .FirstOrDefaultAsync(c => c.Id == chapterId && c.Book.OwnerId == userId, cancellationToken);

        if (chapter == null)
            throw new NotFoundException(nameof(Chapter), chapterId);

        return new ChapterDetailDto(
            chapter.Id,
            chapter.BookId,
            chapter.Order,
            chapter.Title,
            chapter.RawText,
            chapter.Chunks.Count,
            chapter.CreatedAtUtc
        );
    }

    public async Task<ChapterDetailDto> UpdateChapterAsync(Guid chapterId, UpdateChapterRequest request, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;

        var chapter = await _dbContext.Chapters
            .Include(c => c.Book)
            .Include(c => c.Chunks)
            .FirstOrDefaultAsync(c => c.Id == chapterId && c.Book.OwnerId == userId, cancellationToken);

        if (chapter == null)
            throw new NotFoundException(nameof(Chapter), chapterId);

        if (chapter.Book.Status != BookStatus.NeedsChapterReview && chapter.Book.Status != BookStatus.Ready)
        {
            throw new DomainException($"Cannot edit chapter while book status is '{chapter.Book.Status}'.");
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            chapter.Title = request.Title.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.RawText))
        {
            chapter.RawText = request.RawText;
            // If text was modified after embedding, mark book as needing re-review/re-embedding
            if (chapter.Book.Status == BookStatus.Ready)
            {
                chapter.Book.Status = BookStatus.NeedsChapterReview;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ChapterDetailDto(
            chapter.Id,
            chapter.BookId,
            chapter.Order,
            chapter.Title,
            chapter.RawText,
            chapter.Chunks.Count,
            chapter.CreatedAtUtc
        );
    }

    public async Task<ChapterDetailDto> MergeChaptersAsync(Guid bookId, MergeChaptersRequest request, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;

        if (request.ChapterIds == null || request.ChapterIds.Count < 2)
            throw new ValidationException("At least 2 chapters must be selected to merge.");

        var book = await _dbContext.Books
            .Include(b => b.Chapters)
            .FirstOrDefaultAsync(b => b.Id == bookId && b.OwnerId == userId, cancellationToken);

        if (book == null)
            throw new NotFoundException(nameof(Book), bookId);

        var chaptersToMerge = book.Chapters
            .Where(c => request.ChapterIds.Contains(c.Id))
            .OrderBy(c => c.Order)
            .ToList();

        if (chaptersToMerge.Count != request.ChapterIds.Count)
            throw new ValidationException("One or more chapters to merge could not be found.");

        var targetChapter = chaptersToMerge.First();
        var mergedTitle = !string.IsNullOrWhiteSpace(request.NewTitle)
            ? request.NewTitle.Trim()
            : targetChapter.Title;

        var mergedText = string.Join("\n\n--- Merged Section ---\n\n", chaptersToMerge.Select(c => c.RawText));

        targetChapter.Title = mergedTitle;
        targetChapter.RawText = mergedText;

        // Remove the other chapters
        var otherChapters = chaptersToMerge.Skip(1).ToList();
        foreach (var ch in otherChapters)
        {
            _dbContext.Chapters.Remove(ch);
            book.Chapters.Remove(ch);
        }

        // Re-sequence remaining chapter orders
        var ordered = book.Chapters.OrderBy(c => c.Order).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Order = i + 1;
        }

        book.Status = BookStatus.NeedsChapterReview;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ChapterDetailDto(
            targetChapter.Id,
            targetChapter.BookId,
            targetChapter.Order,
            targetChapter.Title,
            targetChapter.RawText,
            0,
            targetChapter.CreatedAtUtc
        );
    }

    public async Task<List<ChapterDetailDto>> SplitChapterAsync(Guid chapterId, SplitChapterRequest request, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;

        var chapter = await _dbContext.Chapters
            .Include(c => c.Book)
                .ThenInclude(b => b.Chapters)
            .FirstOrDefaultAsync(c => c.Id == chapterId && c.Book.OwnerId == userId, cancellationToken);

        if (chapter == null)
            throw new NotFoundException(nameof(Chapter), chapterId);

        var book = chapter.Book;

        int splitIndex = -1;
        if (!string.IsNullOrEmpty(request.SplitMarker))
        {
            splitIndex = chapter.RawText.IndexOf(request.SplitMarker, StringComparison.OrdinalIgnoreCase);
        }

        if (splitIndex <= 0 || splitIndex >= chapter.RawText.Length)
        {
            // Default split near middle on paragraph or sentence boundary
            var mid = chapter.RawText.Length / 2;
            var boundary = chapter.RawText.IndexOf("\n\n", mid, StringComparison.Ordinal);
            if (boundary == -1) boundary = chapter.RawText.IndexOf(". ", mid, StringComparison.Ordinal);
            splitIndex = (boundary != -1) ? boundary : mid;
        }

        var part1Text = chapter.RawText[..splitIndex].Trim();
        var part2Text = chapter.RawText[splitIndex..].Trim();

        chapter.Title = !string.IsNullOrWhiteSpace(request.FirstChapterTitle)
            ? request.FirstChapterTitle.Trim()
            : $"{chapter.Title} (Part 1)";
        chapter.RawText = part1Text;

        // Shift subsequent chapters up by 1
        foreach (var other in book.Chapters.Where(c => c.Order > chapter.Order))
        {
            other.Order++;
        }

        var newChapter = new Chapter
        {
            BookId = book.Id,
            Order = chapter.Order + 1,
            Title = !string.IsNullOrWhiteSpace(request.SecondChapterTitle)
                ? request.SecondChapterTitle.Trim()
                : $"{chapter.Title} (Part 2)",
            RawText = part2Text,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Chapters.Add(newChapter);
        book.Status = BookStatus.NeedsChapterReview;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetChaptersAsync(book.Id, cancellationToken);
    }

    public async Task<List<ChapterDetailDto>> ReorderChaptersAsync(Guid bookId, ReorderChaptersRequest request, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;
        var isAdmin = _currentUserService.IsAdmin;

        var book = await _dbContext.Books
            .Include(b => b.Chapters)
            .FirstOrDefaultAsync(b => b.Id == bookId && (isAdmin || b.OwnerId == userId), cancellationToken);

        if (book == null)
            throw new NotFoundException(nameof(Book), bookId);

        var orderMap = request.Chapters.ToDictionary(x => x.ChapterId, x => x.NewOrder);

        foreach (var chapter in book.Chapters)
        {
            if (orderMap.TryGetValue(chapter.Id, out var newOrder))
            {
                chapter.Order = newOrder;
            }
        }

        // Re-sequence to ensure contiguous 1..N order
        var sorted = book.Chapters.OrderBy(c => c.Order).ToList();
        for (var i = 0; i < sorted.Count; i++)
        {
            sorted[i].Order = i + 1;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetChaptersAsync(bookId, cancellationToken);
    }

    public async Task ConfirmChaptersAsync(Guid bookId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;
        var isAdmin = _currentUserService.IsAdmin;

        var book = await _dbContext.Books
            .Include(b => b.Chapters)
            .FirstOrDefaultAsync(b => b.Id == bookId && (isAdmin || b.OwnerId == userId), cancellationToken);

        if (book == null)
            throw new NotFoundException(nameof(Book), bookId);

        if (book.Chapters.Count == 0)
        {
            throw new ValidationException("Book cannot be confirmed without any chapters.");
        }

        book.Status = BookStatus.Embedding;
        book.FailureReason = null;
        book.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Enqueue background embedding task
        await _ingestionQueue.EnqueueAsync(new IngestionTask(book.Id, IngestionStep.GenerateEmbeddings), cancellationToken);
    }
}

using CaseInterview.Application.Common.Interfaces;
using CaseInterview.Application.Features.Books.DTOs;
using CaseInterview.Application.Features.Books.Services;
using CaseInterview.Domain.Entities;
using CaseInterview.Domain.Enums;
using CaseInterview.Domain.Exceptions;
using CaseInterview.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CaseInterview.UnitTests;

public class BookServiceTests
{
    private readonly CaseInterviewDbContext _dbContext;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IFileStorageService> _fileStorageMock = new();
    private readonly Mock<IIngestionQueue> _ingestionQueueMock = new();
    private readonly BookService _bookService;
    private readonly Guid _currentUserId = Guid.NewGuid();

    public BookServiceTests()
    {
        var options = new DbContextOptionsBuilder<CaseInterviewDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new CaseInterviewDbContext(options);

        _currentUserServiceMock.Setup(u => u.UserId).Returns(_currentUserId);
        _currentUserServiceMock.Setup(u => u.IsAuthenticated).Returns(true);

        _fileStorageMock.Setup(f => f.SaveFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/uploads/test.pdf");

        _bookService = new BookService(_dbContext, _currentUserServiceMock.Object, _fileStorageMock.Object, _ingestionQueueMock.Object);
    }

    [Fact]
    public async Task MergeChapters_CombinesTextAndAdjustsOrder()
    {
        var book = new Book
        {
            OwnerId = _currentUserId,
            Title = "Designing Data-Intensive Applications",
            Status = BookStatus.NeedsChapterReview,
            FilePath = "/path/book.pdf"
        };
        _dbContext.Books.Add(book);

        var ch1 = new Chapter { BookId = book.Id, Order = 1, Title = "Reliability", RawText = "Reliability content." };
        var ch2 = new Chapter { BookId = book.Id, Order = 2, Title = "Scalability", RawText = "Scalability content." };
        var ch3 = new Chapter { BookId = book.Id, Order = 3, Title = "Maintainability", RawText = "Maintainability content." };
        _dbContext.Chapters.AddRange(ch1, ch2, ch3);
        await _dbContext.SaveChangesAsync();

        var mergeRequest = new MergeChaptersRequest(
            ChapterIds: new List<Guid> { ch1.Id, ch2.Id },
            NewTitle: "Reliability & Scalability"
        );

        var merged = await _bookService.MergeChaptersAsync(book.Id, mergeRequest);

        merged.Title.Should().Be("Reliability & Scalability");
        merged.RawText.Should().Contain("Reliability content.").And.Contain("Scalability content.");

        var remainingChapters = await _dbContext.Chapters
            .Where(c => c.BookId == book.Id)
            .OrderBy(c => c.Order)
            .ToListAsync();

        remainingChapters.Should().HaveCount(2);
        remainingChapters[0].Order.Should().Be(1);
        remainingChapters[0].Title.Should().Be("Reliability & Scalability");
        remainingChapters[1].Order.Should().Be(2);
        remainingChapters[1].Title.Should().Be("Maintainability");
    }

    [Fact]
    public async Task SplitChapter_DividesChapterIntoTwoAndShiftsSubsequentOrders()
    {
        var book = new Book
        {
            OwnerId = _currentUserId,
            Title = "System Design Volume 1",
            Status = BookStatus.NeedsChapterReview,
            FilePath = "/path/vol1.pdf"
        };
        _dbContext.Books.Add(book);

        var ch1 = new Chapter
        {
            BookId = book.Id,
            Order = 1,
            Title = "Big Chapter",
            RawText = "First part about SQL databases. [SPLIT_HERE] Second part about NoSQL key-value stores."
        };
        var ch2 = new Chapter { BookId = book.Id, Order = 2, Title = "Next Topic", RawText = "Next topic." };
        _dbContext.Chapters.AddRange(ch1, ch2);
        await _dbContext.SaveChangesAsync();

        var splitRequest = new SplitChapterRequest(
            SplitMarker: "[SPLIT_HERE]",
            FirstChapterTitle: "Relational SQL",
            SecondChapterTitle: "NoSQL Stores"
        );

        var chapters = await _bookService.SplitChapterAsync(ch1.Id, splitRequest);

        chapters.Should().HaveCount(3);
        chapters[0].Order.Should().Be(1);
        chapters[0].Title.Should().Be("Relational SQL");
        chapters[0].RawText.Should().Contain("First part about SQL databases.");

        chapters[1].Order.Should().Be(2);
        chapters[1].Title.Should().Be("NoSQL Stores");
        chapters[1].RawText.Should().Contain("Second part about NoSQL key-value stores.");

        chapters[2].Order.Should().Be(3);
        chapters[2].Title.Should().Be("Next Topic");
    }

    [Fact]
    public async Task ListBooks_OnlyReturnsBooksOwnedByCurrentUser()
    {
        var userBook = new Book { OwnerId = _currentUserId, Title = "User's Book", FilePath = "/p1.pdf" };
        var otherUserBook = new Book { OwnerId = Guid.NewGuid(), Title = "Other User's Book", FilePath = "/p2.pdf" };

        _dbContext.Books.AddRange(userBook, otherUserBook);
        await _dbContext.SaveChangesAsync();

        var books = await _bookService.ListBooksAsync();

        books.Should().HaveCount(1);
        books[0].Title.Should().Be("User's Book");
    }

    [Fact]
    public async Task ConfirmChapters_SetsStatusToEmbeddingAndEnqueuesJob()
    {
        var book = new Book
        {
            OwnerId = _currentUserId,
            Title = "System Architecture",
            Status = BookStatus.NeedsChapterReview,
            FilePath = "/path.pdf"
        };
        _dbContext.Books.Add(book);
        var ch = new Chapter { BookId = book.Id, Order = 1, Title = "Overview", RawText = "Some text" };
        _dbContext.Chapters.Add(ch);
        await _dbContext.SaveChangesAsync();

        await _bookService.ConfirmChaptersAsync(book.Id);

        var updatedBook = await _dbContext.Books.FindAsync(book.Id);
        updatedBook!.Status.Should().Be(BookStatus.Embedding);

        _ingestionQueueMock.Verify(q => q.EnqueueAsync(
            It.Is<IngestionTask>(t => t.BookId == book.Id && t.Step == IngestionStep.GenerateEmbeddings),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

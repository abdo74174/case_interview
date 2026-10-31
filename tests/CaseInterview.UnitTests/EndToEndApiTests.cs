using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CaseInterview.Application.Features.Auth.DTOs;
using CaseInterview.Application.Features.Books.DTOs;
using CaseInterview.Application.Features.Search.DTOs;
using CaseInterview.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace CaseInterview.UnitTests;

public class EndToEndApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string DbName = $"TestE2EDb_{Guid.NewGuid():N}";

    public EndToEndApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(Microsoft.EntityFrameworkCore.DbContextOptions<CaseInterview.Infrastructure.Persistence.CaseInterviewDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<CaseInterview.Infrastructure.Persistence.CaseInterviewDbContext>(options =>
                {
                    options.UseInMemoryDatabase(DbName);
                });
            });
        });
    }

    private static byte[] CreateSamplePdfBytes()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        // Page 1: Chapter 1
        var page1 = builder.AddPage(595, 842);
        page1.AddText("Chapter 1. Rate Limiting Architecture", 14, new PdfPoint(50, 750), font);
        page1.AddText("Rate limiting protects backend services from abuse and denial-of-service attacks.", 12, new PdfPoint(50, 700), font);
        page1.AddText("A sliding window counter algorithm maintains counters across short intervals for high precision.", 12, new PdfPoint(50, 670), font);

        // Page 2: Chapter 2
        var page2 = builder.AddPage(595, 842);
        page2.AddText("Chapter 2. Distributed Caching Strategies", 14, new PdfPoint(50, 750), font);
        page2.AddText("Distributed caching uses Redis or Memcached clusters to minimize database read traffic.", 12, new PdfPoint(50, 700), font);
        page2.AddText("Cache-aside and write-through patterns provide different tradeoffs between consistency and performance.", 12, new PdfPoint(50, 670), font);

        return builder.Build();
    }

    [Fact]
    public async Task FullLifecycle_AllEndpointsWorkSuccessfully()
    {
        var client = _factory.CreateClient();

        // -------------------------------------------------------------
        // 1. Auth: Register (as Admin for management lifecycle)
        // -------------------------------------------------------------
        var uniqueEmail = $"e2e_{Guid.NewGuid():N}@systemdesign.io";
        var registerRequest = new RegisterRequest(uniqueEmail, "SecurePassword123!", "System Architect", "Admin");
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", registerRequest);
        var regErr = await registerResponse.Content.ReadAsStringAsync();
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK, regErr);
        var authResult = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions);
        authResult.Should().NotBeNull();
        authResult!.Token.Should().NotBeNullOrWhiteSpace();
        authResult.Email.Should().Be(uniqueEmail);

        // -------------------------------------------------------------
        // 2. Auth: Login
        // -------------------------------------------------------------
        var loginRequest = new LoginRequest(uniqueEmail, "SecurePassword123!");
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginErr = await loginResponse.Content.ReadAsStringAsync();
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK, loginErr);

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions);
        loginResult.Should().NotBeNull();
        loginResult!.Token.Should().NotBeNullOrWhiteSpace();

        // Attach JWT token to all subsequent requests
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult.Token);

        // -------------------------------------------------------------
        // 3. Books: Upload PDF (Multipart)
        // -------------------------------------------------------------
        var pdfBytes = CreateSamplePdfBytes();
        try { File.WriteAllBytes("C:/Users/pc/.gemini/antigravity/brain/b9ca98ee-184a-46f2-9ea4-12c32ee13549/scratch/sample.pdf", pdfBytes); } catch { }
        using var multipartContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        multipartContent.Add(fileContent, "file", "SystemDesignGuide.pdf");
        multipartContent.Add(new StringContent("System Design Mastery"), "title");
        multipartContent.Add(new StringContent("Principal Architect"), "author");

        var uploadResponse = await client.PostAsync("/api/books", multipartContent);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Created, "PDF upload should return 201 Created");

        var uploadedBook = await uploadResponse.Content.ReadFromJsonAsync<BookDto>(_jsonOptions);
        uploadedBook.Should().NotBeNull();
        uploadedBook!.Id.Should().NotBeEmpty();
        uploadedBook.Title.Should().Be("System Design Mastery");

        var bookId = uploadedBook.Id;

        // Allow background service a moment to extract chapters from PDF
        var maxWait = TimeSpan.FromSeconds(10);
        var start = DateTime.UtcNow;
        BookDetailDto? bookDetail = null;

        while (DateTime.UtcNow - start < maxWait)
        {
            var getDetailRes = await client.GetAsync($"/api/books/{bookId}");
            getDetailRes.StatusCode.Should().Be(HttpStatusCode.OK);
            bookDetail = await getDetailRes.Content.ReadFromJsonAsync<BookDetailDto>(_jsonOptions);

            if (bookDetail != null && bookDetail.Status == BookStatus.NeedsChapterReview)
            {
                break;
            }
            await Task.Delay(500);
        }

        bookDetail.Should().NotBeNull();
        bookDetail!.Status.Should().Be(BookStatus.NeedsChapterReview, "Book extraction should complete and enter review status");
        bookDetail.Chapters.Should().NotBeEmpty("Extracted chapters should be present");

        // -------------------------------------------------------------
        // 4. Books: List User's Books
        // -------------------------------------------------------------
        var listResponse = await client.GetAsync("/api/books");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var books = await listResponse.Content.ReadFromJsonAsync<List<BookDto>>(_jsonOptions);
        books.Should().NotBeNull();
        books!.Should().Contain(b => b.Id == bookId);

        // -------------------------------------------------------------
        // 5. Books: Get Book Details
        // -------------------------------------------------------------
        var getResponse = await client.GetAsync($"/api/books/{bookId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // -------------------------------------------------------------
        // 6. Chapters: List Chapters for Book
        // -------------------------------------------------------------
        var chaptersResponse = await client.GetAsync($"/api/books/{bookId}/chapters");
        chaptersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var chapters = await chaptersResponse.Content.ReadFromJsonAsync<List<ChapterDetailDto>>(_jsonOptions);
        chapters.Should().NotBeNull();
        chapters!.Count.Should().BeGreaterThan(0);

        var firstChapter = chapters[0];

        // -------------------------------------------------------------
        // 7. Chapters: Get Single Chapter Detail
        // -------------------------------------------------------------
        var singleChapterRes = await client.GetAsync($"/api/chapters/{firstChapter.Id}");
        singleChapterRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var singleChapter = await singleChapterRes.Content.ReadFromJsonAsync<ChapterDetailDto>(_jsonOptions);
        singleChapter.Should().NotBeNull();
        singleChapter!.Id.Should().Be(firstChapter.Id);

        // -------------------------------------------------------------
        // 8. Chapters: Update/Rename Chapter
        // -------------------------------------------------------------
        var updateRequest = new UpdateChapterRequest(
            Title: "Renamed Chapter 1: Advanced Rate Limiting",
            RawText: "Rate limiting is essential for high-availability distributed systems. Token bucket and leaky bucket are standard algorithms."
        );
        var updateRes = await client.PutAsJsonAsync($"/api/chapters/{firstChapter.Id}", updateRequest);
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedChapter = await updateRes.Content.ReadFromJsonAsync<ChapterDetailDto>(_jsonOptions);
        updatedChapter.Should().NotBeNull();
        updatedChapter!.Title.Should().Be("Renamed Chapter 1: Advanced Rate Limiting");

        // -------------------------------------------------------------
        // 9. Chapters: Split Chapter
        // -------------------------------------------------------------
        var splitRequest = new SplitChapterRequest(
            SplitMarker: "Token bucket",
            FirstChapterTitle: "Rate Limiting Foundations",
            SecondChapterTitle: "Token Bucket Deep Dive"
        );
        var splitRes = await client.PostAsJsonAsync($"/api/chapters/{firstChapter.Id}/split", splitRequest);
        splitRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var chaptersAfterSplit = await splitRes.Content.ReadFromJsonAsync<List<ChapterDetailDto>>(_jsonOptions);
        chaptersAfterSplit.Should().NotBeNull();
        chaptersAfterSplit!.Count.Should().Be(chapters.Count + 1);

        // -------------------------------------------------------------
        // 10. Chapters: Merge Chapters
        // -------------------------------------------------------------
        var chaptersToMerge = chaptersAfterSplit.Take(2).Select(c => c.Id).ToList();
        var mergeRequest = new MergeChaptersRequest(chaptersToMerge, "Merged Foundations & Deep Dive");
        var mergeRes = await client.PostAsJsonAsync($"/api/books/{bookId}/chapters/merge", mergeRequest);
        mergeRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var mergedChapter = await mergeRes.Content.ReadFromJsonAsync<ChapterDetailDto>(_jsonOptions);
        mergedChapter.Should().NotBeNull();
        mergedChapter!.Title.Should().Be("Merged Foundations & Deep Dive");

        // -------------------------------------------------------------
        // 11. Chapters: Reorder Chapters
        // -------------------------------------------------------------
        var currentChaptersRes = await client.GetAsync($"/api/books/{bookId}/chapters");
        var currentChapters = await currentChaptersRes.Content.ReadFromJsonAsync<List<ChapterDetailDto>>(_jsonOptions);
        currentChapters.Should().NotBeNull();

        var reorderItems = currentChapters!.Select((c, idx) => new ReorderChapterItem(c.Id, currentChapters.Count - idx)).ToList();
        var reorderRequest = new ReorderChaptersRequest(reorderItems);
        var reorderRes = await client.PutAsJsonAsync($"/api/books/{bookId}/chapters/reorder", reorderRequest);
        reorderRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // -------------------------------------------------------------
        // 12. Books: Confirm Chapters -> Starts Embedding Pipeline
        // -------------------------------------------------------------
        var confirmRes = await client.PostAsync($"/api/books/{bookId}/confirm", null);
        confirmRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Wait for background embedding service to chunk & embed
        start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < maxWait)
        {
            var getDetailRes = await client.GetAsync($"/api/books/{bookId}");
            bookDetail = await getDetailRes.Content.ReadFromJsonAsync<BookDetailDto>(_jsonOptions);

            if (bookDetail != null && bookDetail.Status == BookStatus.Ready)
            {
                break;
            }
            await Task.Delay(500);
        }

        bookDetail!.Status.Should().Be(BookStatus.Ready, "Book should transition to Ready after background embedding completes");

        // -------------------------------------------------------------
        // 13. Search: Vector Cosine Semantic Search
        // -------------------------------------------------------------
        var searchRequest = new BookSearchRequest("rate limiting and token bucket", TopK: 3);
        var searchRes = await client.PostAsJsonAsync($"/api/books/{bookId}/search", searchRequest);
        searchRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var searchResult = await searchRes.Content.ReadFromJsonAsync<BookSearchResultDto>(_jsonOptions);
        searchResult.Should().NotBeNull();
        searchResult!.BookId.Should().Be(bookId);
        searchResult.Results.Should().NotBeEmpty("Search should retrieve relevant embedded chunks");
        searchResult.Results[0].Similarity.Should().BeGreaterThan(0.0);
        searchResult.Results[0].ChapterTitle.Should().NotBeNullOrWhiteSpace();

        // -------------------------------------------------------------
        // 14. Books: Delete Book
        // -------------------------------------------------------------
        var deleteRes = await client.DeleteAsync($"/api/books/{bookId}");
        deleteRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify book is deleted
        var getDeletedRes = await client.GetAsync($"/api/books/{bookId}");
        getDeletedRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NormalUser_CannotUploadBook_CanOnlyViewBooks()
    {
        var client = _factory.CreateClient();

        // 1. Register normal candidate user (Role: "User")
        var userEmail = $"candidate_{Guid.NewGuid():N}@caseinterview.io";
        var registerRequest = new RegisterRequest(userEmail, "CandidatePass123!", "Interview Candidate", "User");
        var regResponse = await client.PostAsJsonAsync("/api/auth/register", registerRequest);
        regResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var authResult = await regResponse.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions);
        authResult.Should().NotBeNull();
        authResult!.Role.Should().Be("User");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResult.Token);

        // 2. Candidate user CAN view books list
        var listRes = await client.GetAsync("/api/books");
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Candidate user CANNOT upload book -> 403 Forbidden
        var pdfBytes = CreateSamplePdfBytes();
        using var multipartContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        multipartContent.Add(fileContent, "file", "CandidateAttempt.pdf");
        multipartContent.Add(new StringContent("Candidate Book"), "title");

        var uploadRes = await client.PostAsync("/api/books", multipartContent);
        uploadRes.StatusCode.Should().Be(HttpStatusCode.Forbidden, "Regular users must not be permitted to upload books");
    }
}

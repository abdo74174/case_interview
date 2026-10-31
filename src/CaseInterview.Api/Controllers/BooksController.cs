using CaseInterview.Application.Features.Books.DTOs;
using CaseInterview.Application.Features.Books.Services;
using CaseInterview.Application.Features.Search.DTOs;
using CaseInterview.Application.Features.Search.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaseInterview.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BooksController : ControllerBase
{
    private readonly IBookService _bookService;
    private readonly ISearchService _searchService;
    private readonly IValidator<BookSearchRequest> _searchValidator;

    public BooksController(
        IBookService bookService,
        ISearchService searchService,
        IValidator<BookSearchRequest> searchValidator)
    {
        _bookService = bookService;
        _searchService = searchService;
        _searchValidator = searchValidator;
    }

    [HttpPost]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(BookDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadBook(
        IFormFile file,
        [FromForm] string? title,
        [FromForm] string? author,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid File",
                Detail = "A non-empty PDF file must be provided."
            });
        }

        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid File Type",
                Detail = "Only PDF documents (.pdf) are supported."
            });
        }

        await using var stream = file.OpenReadStream();
        var result = await _bookService.UploadBookAsync(
            stream,
            file.FileName,
            file.Length,
            title,
            author,
            cancellationToken);

        return CreatedAtAction(nameof(GetBook), new { id = result.Id }, result);
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<BookDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListBooks(CancellationToken cancellationToken)
    {
        var books = await _bookService.ListBooksAsync(cancellationToken);
        return Ok(books);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(BookDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBook(Guid id, CancellationToken cancellationToken)
    {
        var book = await _bookService.GetBookAsync(id, cancellationToken);
        return Ok(book);
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBook(Guid id, CancellationToken cancellationToken)
    {
        await _bookService.DeleteBookAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/chapters")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<ChapterDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChapters(Guid id, CancellationToken cancellationToken)
    {
        var chapters = await _bookService.GetChaptersAsync(id, cancellationToken);
        return Ok(chapters);
    }

    [HttpPost("{id:guid}/chapters/merge")]
    [Authorize]
    [ProducesResponseType(typeof(ChapterDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MergeChapters(
        Guid id,
        [FromBody] MergeChaptersRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _bookService.MergeChaptersAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:guid}/chapters/reorder")]
    [Authorize]
    [ProducesResponseType(typeof(List<ChapterDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReorderChapters(
        Guid id,
        [FromBody] ReorderChaptersRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _bookService.ReorderChaptersAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/confirm")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmChapters(Guid id, CancellationToken cancellationToken)
    {
        await _bookService.ConfirmChaptersAsync(id, cancellationToken);
        return Ok(new { message = "Chapters confirmed. Embedding generation started in background." });
    }

    [HttpPost("{id:guid}/search")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(BookSearchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SearchBook(
        Guid id,
        [FromBody] BookSearchRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _searchValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            throw new Domain.Exceptions.ValidationException(errors);
        }

        var result = await _searchService.SearchBookChunksAsync(id, request, cancellationToken);
        return Ok(result);
    }
}

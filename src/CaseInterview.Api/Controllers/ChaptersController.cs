using CaseInterview.Application.Features.Books.DTOs;
using CaseInterview.Application.Features.Books.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaseInterview.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChaptersController : ControllerBase
{
    private readonly IBookService _bookService;

    public ChaptersController(IBookService bookService)
    {
        _bookService = bookService;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ChapterDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChapter(Guid id, CancellationToken cancellationToken)
    {
        var chapter = await _bookService.GetChapterAsync(id, cancellationToken);
        return Ok(chapter);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ChapterDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateChapter(
        Guid id,
        [FromBody] UpdateChapterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _bookService.UpdateChapterAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/split")]
    [ProducesResponseType(typeof(List<ChapterDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SplitChapter(
        Guid id,
        [FromBody] SplitChapterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _bookService.SplitChapterAsync(id, request, cancellationToken);
        return Ok(result);
    }
}

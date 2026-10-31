using CaseInterview.Application.Common.Models;

namespace CaseInterview.Application.Common.Interfaces;

public interface IPdfExtractor
{
    Task<List<ExtractedChapter>> ExtractChaptersAsync(Stream pdfStream, CancellationToken cancellationToken = default);
}

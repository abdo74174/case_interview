using System.Text;
using System.Text.RegularExpressions;
using CaseInterview.Application.Common.Interfaces;
using CaseInterview.Application.Common.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Outline;

namespace CaseInterview.Infrastructure.Pdf;

public partial class PdfPigExtractor : IPdfExtractor
{
    [GeneratedRegex(@"(?m)^(?:\s*)(?:(Chapter\s+(?:\d+|[IVXLCDM]+)[\s:.\-–—]+[^\r\n]+)|(\d{1,2}\.[\s\t]+[A-Z][^\r\n]{3,80})|(Part\s+(?:\d+|[IVXLCDM]+)[\s:.\-–—]+[^\r\n]+))\s*$", RegexOptions.Compiled)]
    private static partial Regex ChapterHeadingRegex();

    public Task<List<ExtractedChapter>> ExtractChaptersAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        // Copy to MemoryStream to support seeking if needed by PdfPig
        using var memoryStream = new MemoryStream();
        pdfStream.CopyTo(memoryStream);
        memoryStream.Position = 0;

        using var document = PdfDocument.Open(memoryStream);
        var pageCount = document.NumberOfPages;

        if (pageCount == 0)
        {
            return Task.FromResult(new List<ExtractedChapter>());
        }

        // 1. Try to detect chapters from PDF Bookmarks (Table of Contents Outline)
        if (document.TryGetBookmarks(out var bookmarks) && bookmarks != null && bookmarks.Roots.Any())
        {
            var chaptersFromBookmarks = ExtractFromBookmarks(document, bookmarks.Roots);
            if (chaptersFromBookmarks.Count > 0)
            {
                return Task.FromResult(chaptersFromBookmarks);
            }
        }

        // 2. Fallback: Extract all pages and detect headings via Regex
        var fullTextBuilder = new StringBuilder();
        var pageTexts = new List<string>(pageCount);

        for (var pageNum = 1; pageNum <= pageCount; pageNum++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = document.GetPage(pageNum);
            var text = page.Text ?? string.Empty;
            pageTexts.Add(text);
            fullTextBuilder.AppendLine(text);
        }

        var fullText = fullTextBuilder.ToString();
        var chaptersFromRegex = ExtractFromHeadingsRegex(fullText);

        if (chaptersFromRegex.Count > 0)
        {
            return Task.FromResult(chaptersFromRegex);
        }

        // 3. Fallback: If no bookmarks and no chapter headings found, bundle into chapters of 15 pages or single chapter
        var fallbackChapters = new List<ExtractedChapter>();
        const int pagesPerChapter = 15;

        for (var i = 0; i < pageCount; i += pagesPerChapter)
        {
            var chapterIndex = (i / pagesPerChapter) + 1;
            var endPage = Math.Min(i + pagesPerChapter, pageCount);
            var chapterSb = new StringBuilder();

            for (var p = i; p < endPage; p++)
            {
                chapterSb.AppendLine(pageTexts[p]);
            }

            var rawText = chapterSb.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(rawText))
            {
                fallbackChapters.Add(new ExtractedChapter(
                    Order: chapterIndex,
                    Title: pageCount <= pagesPerChapter ? "Full Content" : $"Section {chapterIndex} (Pages {i + 1}-{endPage})",
                    RawText: rawText
                ));
            }
        }

        return Task.FromResult(fallbackChapters);
    }

    private static List<ExtractedChapter> ExtractFromBookmarks(PdfDocument document, IReadOnlyList<BookmarkNode> rootNodes)
    {
        var flatBookmarks = new List<(string Title, int PageNumber)>();

        void Flatten(IReadOnlyList<BookmarkNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node is DocumentBookmarkNode bookmark && bookmark.PageNumber >= 1)
                {
                    flatBookmarks.Add((bookmark.Title.Trim(), bookmark.PageNumber));
                }

                if (node.Children.Count > 0)
                {
                    Flatten(node.Children);
                }
            }
        }

        Flatten(rootNodes);

        if (flatBookmarks.Count == 0) return new List<ExtractedChapter>();

        // Sort by page number and eliminate duplicate pages
        var sorted = flatBookmarks
            .OrderBy(b => b.PageNumber)
            .DistinctBy(b => b.PageNumber)
            .ToList();

        var result = new List<ExtractedChapter>();
        var totalPages = document.NumberOfPages;

        for (var i = 0; i < sorted.Count; i++)
        {
            var current = sorted[i];
            var startPage = current.PageNumber;
            var endPage = (i + 1 < sorted.Count) ? sorted[i + 1].PageNumber - 1 : totalPages;

            if (endPage < startPage) endPage = startPage;

            var sb = new StringBuilder();
            for (var pageNum = startPage; pageNum <= Math.Min(endPage, totalPages); pageNum++)
            {
                var page = document.GetPage(pageNum);
                sb.AppendLine(page.Text);
            }

            var text = sb.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                result.Add(new ExtractedChapter(
                    Order: result.Count + 1,
                    Title: current.Title,
                    RawText: text
                ));
            }
        }

        return result;
    }

    private static List<ExtractedChapter> ExtractFromHeadingsRegex(string fullText)
    {
        var matches = ChapterHeadingRegex().Matches(fullText);
        if (matches.Count == 0) return new List<ExtractedChapter>();

        var result = new List<ExtractedChapter>();

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var title = match.Value.Trim();
            var startIndex = match.Index;
            var endIndex = (i + 1 < matches.Count) ? matches[i + 1].Index : fullText.Length;

            var chapterText = fullText[startIndex..endIndex].Trim();
            if (!string.IsNullOrWhiteSpace(chapterText))
            {
                result.Add(new ExtractedChapter(
                    Order: result.Count + 1,
                    Title: title,
                    RawText: chapterText
                ));
            }
        }

        return result;
    }
}

using CaseInterview.Infrastructure.Chunker;
using FluentAssertions;
using Xunit;

namespace CaseInterview.UnitTests;

public class SemanticTextChunkerTests
{
    private readonly SemanticTextChunker _chunker = new();

    [Fact]
    public void ChunkText_WhenInputIsEmptyOrWhitespace_ReturnsEmptyList()
    {
        var result1 = _chunker.ChunkText(string.Empty);
        var result2 = _chunker.ChunkText("   ");
        var result3 = _chunker.ChunkText(null!);

        result1.Should().BeEmpty();
        result2.Should().BeEmpty();
        result3.Should().BeEmpty();
    }

    [Fact]
    public void ChunkText_WhenTextIsShort_ReturnsSingleChunk()
    {
        var text = "Designing a distributed rate limiter requires sliding window counter algorithm. It operates on Redis cluster.";

        var chunks = _chunker.ChunkText(text, minTokens: 300, maxTokens: 500, overlapTokens: 50);

        chunks.Should().HaveCount(1);
        chunks[0].Index.Should().Be(0);
        chunks[0].Text.Should().Be(text);
        chunks[0].TokenCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ChunkText_WhenTextExceedsMaxTokens_CreatesMultipleChunksWithOverlap()
    {
        // Generate substantial text: multiple paragraphs with sentences
        var paragraph = "Distributed caching improves read throughput significantly by keeping hot data in volatile RAM. " +
                        "Consistent hashing minimizes reorganization when cache nodes scale up or down dynamically. " +
                        "A cache-aside pattern guarantees that applications read from cache first and fall back to database storage on miss. " +
                        "Cache invalidation remains one of the two hardest problems in computer science along with naming things. " +
                        "Write-through caching updates cache and database synchronously to maintain immediate read consistency. ";

        var repeatedText = string.Join("\n\n", Enumerable.Repeat(paragraph, 12));

        var chunks = _chunker.ChunkText(repeatedText, minTokens: 100, maxTokens: 200, overlapTokens: 30);

        chunks.Should().HaveCountGreaterThan(1);

        for (var i = 0; i < chunks.Count; i++)
        {
            chunks[i].Index.Should().Be(i);
            chunks[i].Text.Should().NotBeNullOrWhiteSpace();
            chunks[i].TokenCount.Should().BeGreaterThan(0);
        }

        // Verify overlap: words from the tail of chunk 0 should appear in chunk 1
        var chunk0Words = chunks[0].Text.Split(' ').TakeLast(5).ToList();
        var overlapFound = chunk0Words.Any(w => chunks[1].Text.Contains(w, StringComparison.OrdinalIgnoreCase));
        overlapFound.Should().BeTrue("consecutive chunks must maintain token overlap");
    }

    [Fact]
    public void EstimateTokenCount_ReturnsSensibleTokenEstimate()
    {
        var sample = "Kafka is a distributed event store and stream-processing platform.";
        var tokenCount = SemanticTextChunker.EstimateTokenCount(sample);

        // 8 words, approx 9-15 tokens
        tokenCount.Should().BeInRange(8, 20);
    }
}

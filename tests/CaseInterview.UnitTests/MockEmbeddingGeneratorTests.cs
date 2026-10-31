using CaseInterview.Infrastructure.AI;
using FluentAssertions;
using Xunit;

namespace CaseInterview.UnitTests;

public class MockEmbeddingGeneratorTests
{
    private readonly MockEmbeddingGenerator _generator = new();

    [Fact]
    public async Task GenerateAsync_ProducesNormalized1536DimensionVector()
    {
        var texts = new[] { "System Design Interview: Rate Limiter Architecture" };

        var result = await _generator.GenerateAsync(texts);

        result.Should().HaveCount(1);
        var vector = result[0].Vector;
        vector.Length.Should().Be(1536);

        // Vector should be unit normalized: sum of squares ≈ 1.0
        float sumSquares = 0f;
        var array = vector.ToArray();
        for (var i = 0; i < array.Length; i++)
        {
            sumSquares += array[i] * array[i];
        }

        sumSquares.Should().BeApproximately(1.0f, 0.01f);
    }

    [Fact]
    public async Task GenerateAsync_IsDeterministicForSameInput()
    {
        var text = "Design a URL shortener like TinyURL";

        var result1 = await _generator.GenerateAsync(new[] { text });
        var result2 = await _generator.GenerateAsync(new[] { text });

        var v1 = result1[0].Vector.ToArray();
        var v2 = result2[0].Vector.ToArray();

        v1.Should().Equal(v2);
    }
}

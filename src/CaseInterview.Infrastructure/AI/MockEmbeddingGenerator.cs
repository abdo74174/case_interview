using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;

namespace CaseInterview.Infrastructure.AI;

/// <summary>
/// Deterministic mock embedding generator for local offline development, unit tests, or CI
/// when an external LLM / OpenAI API key is not configured.
/// Produces normalized 1536-dimensional unit vectors.
/// </summary>
public class MockEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private const int Dimensions = 1536;

    public EmbeddingGeneratorMetadata Metadata => new("MockEmbeddingGenerator");

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var resultList = new List<Embedding<float>>();

        foreach (var val in values)
        {
            var vector = new float[Dimensions];
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(val ?? string.Empty));

            // Generate deterministic pseudo-random float vector from hash
            var seed = BitConverter.ToInt32(bytes, 0);
            var random = new Random(seed);

            float sumSquares = 0f;
            for (var i = 0; i < Dimensions; i++)
            {
                var valFloat = (float)(random.NextDouble() * 2.0 - 1.0);
                vector[i] = valFloat;
                sumSquares += valFloat * valFloat;
            }

            // Normalize vector to unit length
            var norm = (float)Math.Sqrt(sumSquares);
            if (norm > 0)
            {
                for (var i = 0; i < Dimensions; i++)
                {
                    vector[i] /= norm;
                }
            }

            resultList.Add(new Embedding<float>(vector));
        }

        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(resultList));
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}

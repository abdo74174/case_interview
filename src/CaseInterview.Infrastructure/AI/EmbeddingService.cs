using CaseInterview.Application.Common.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CaseInterview.Infrastructure.AI;

public class EmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;
    private readonly ILogger<EmbeddingService> _logger;
    private const int DefaultBatchSize = 25;

    public EmbeddingService(
        IEmbeddingGenerator<string, Embedding<float>> generator,
        ILogger<EmbeddingService> logger)
    {
        _generator = generator;
        _logger = logger;
    }

    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be empty when generating embedding.", nameof(text));

        var response = await _generator.GenerateAsync(new[] { text }, cancellationToken: cancellationToken);
        var embedding = response.FirstOrDefault();

        if (embedding == null || embedding.Vector.IsEmpty)
        {
            throw new InvalidOperationException("Failed to generate embedding vector from the AI provider.");
        }

        return embedding.Vector;
    }

    public async Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsBatchAsync(IList<string> texts, CancellationToken cancellationToken = default)
    {
        if (texts == null || texts.Count == 0)
            return new List<ReadOnlyMemory<float>>();

        var results = new List<ReadOnlyMemory<float>>(texts.Count);

        for (var i = 0; i < texts.Count; i += DefaultBatchSize)
        {
            var batch = texts.Skip(i).Take(DefaultBatchSize).ToList();
            _logger.LogInformation("Generating embeddings for batch of {BatchSize} texts ({Start}-{End} of {Total})",
                batch.Count, i + 1, Math.Min(i + batch.Count, texts.Count), texts.Count);

            var response = await _generator.GenerateAsync(batch, cancellationToken: cancellationToken);

            foreach (var emb in response)
            {
                results.Add(emb.Vector);
            }
        }

        return results;
    }
}

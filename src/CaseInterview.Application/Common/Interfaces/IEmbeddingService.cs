namespace CaseInterview.Application.Common.Interfaces;

public interface IEmbeddingService
{
    Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsBatchAsync(IList<string> texts, CancellationToken cancellationToken = default);
}

namespace CaseInterview.Application.Common.Interfaces;

public enum IngestionStep
{
    ExtractChapters = 1,
    GenerateEmbeddings = 2
}

public sealed record IngestionTask(Guid BookId, IngestionStep Step);

public interface IIngestionQueue
{
    ValueTask EnqueueAsync(IngestionTask task, CancellationToken cancellationToken = default);
    ValueTask<IngestionTask> DequeueAsync(CancellationToken cancellationToken = default);
}

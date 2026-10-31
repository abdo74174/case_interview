namespace CaseInterview.Domain.ValueObjects;

public sealed record RubricItem(
    string Topic,
    string Description,
    int Weight,
    List<Guid> SourceChunkIds
);

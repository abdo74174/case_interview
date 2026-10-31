namespace CaseInterview.Domain.ValueObjects;

public sealed record RubricScore(
    string Topic,
    double Score,
    string Assessment
);

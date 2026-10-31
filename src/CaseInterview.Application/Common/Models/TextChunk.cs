namespace CaseInterview.Application.Common.Models;

public sealed record TextChunk(
    int Index,
    string Text,
    int TokenCount
);

using CaseInterview.Application.Common.Models;

namespace CaseInterview.Application.Common.Interfaces;

public interface ITextChunker
{
    List<TextChunk> ChunkText(string text, int minTokens = 300, int maxTokens = 500, int overlapTokens = 50);
}

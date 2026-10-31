using System.Text.RegularExpressions;
using CaseInterview.Application.Common.Interfaces;
using CaseInterview.Application.Common.Models;

namespace CaseInterview.Infrastructure.Chunker;

public partial class SemanticTextChunker : ITextChunker
{
    [GeneratedRegex(@"(?<=[.!?])\s+", RegexOptions.Compiled)]
    private static partial Regex SentenceSplitRegex();

    public List<TextChunk> ChunkText(string text, int minTokens = 300, int maxTokens = 500, int overlapTokens = 50)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<TextChunk>();

        var paragraphs = text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        var sentences = new List<string>();

        foreach (var paragraph in paragraphs)
        {
            var pSentences = SentenceSplitRegex().Split(paragraph.Trim())
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s));

            sentences.AddRange(pSentences);
        }

        // If no sentence boundaries were detected, split by whitespace words
        if (sentences.Count == 0)
        {
            sentences.Add(text.Trim());
        }

        var chunks = new List<TextChunk>();
        var currentChunkSentences = new List<string>();
        var currentTokenCount = 0;
        var chunkIndex = 0;

        for (var i = 0; i < sentences.Count; i++)
        {
            var sentence = sentences[i];
            var sentenceTokens = EstimateTokenCount(sentence);

            // If a single sentence itself exceeds maxTokens, split it into smaller sub-sentences
            if (sentenceTokens > maxTokens)
            {
                var subWords = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var subSentence = new List<string>();
                var subTokens = 0;

                foreach (var word in subWords)
                {
                    var wordTokens = EstimateTokenCount(word);
                    if (subTokens + wordTokens > maxTokens && subSentence.Count > 0)
                    {
                        var assembled = string.Join(" ", subSentence);
                        currentChunkSentences.Add(assembled);
                        currentTokenCount += subTokens;

                        if (currentTokenCount >= minTokens)
                        {
                            EmitChunk(chunks, currentChunkSentences, currentTokenCount, ref chunkIndex, overlapTokens);
                            currentChunkSentences = RetainOverlapSentences(currentChunkSentences, overlapTokens, out currentTokenCount);
                        }

                        subSentence.Clear();
                        subTokens = 0;
                    }

                    subSentence.Add(word);
                    subTokens += wordTokens;
                }

                if (subSentence.Count > 0)
                {
                    var assembled = string.Join(" ", subSentence);
                    currentChunkSentences.Add(assembled);
                    currentTokenCount += subTokens;
                }

                continue;
            }

            // Normal sentence addition
            if (currentTokenCount + sentenceTokens > maxTokens && currentTokenCount >= minTokens)
            {
                // Emit current chunk
                EmitChunk(chunks, currentChunkSentences, currentTokenCount, ref chunkIndex, overlapTokens);

                // Prepare sliding window with overlap
                currentChunkSentences = RetainOverlapSentences(currentChunkSentences, overlapTokens, out currentTokenCount);
            }

            currentChunkSentences.Add(sentence);
            currentTokenCount += sentenceTokens;
        }

        // Emit final remainder chunk if any sentences remain
        if (currentChunkSentences.Count > 0)
        {
            EmitChunk(chunks, currentChunkSentences, currentTokenCount, ref chunkIndex, overlapTokens);
        }

        return chunks;
    }

    private static void EmitChunk(
        List<TextChunk> chunks,
        List<string> sentences,
        int tokenCount,
        ref int chunkIndex,
        int overlapTokens)
    {
        var chunkText = string.Join(" ", sentences).Trim();
        if (!string.IsNullOrWhiteSpace(chunkText))
        {
            chunks.Add(new TextChunk(
                Index: chunkIndex++,
                Text: chunkText,
                TokenCount: tokenCount
            ));
        }
    }

    private static List<string> RetainOverlapSentences(List<string> sentences, int targetOverlapTokens, out int newTokens)
    {
        var retained = new List<string>();
        newTokens = 0;

        for (var i = sentences.Count - 1; i >= 0; i--)
        {
            var tokens = EstimateTokenCount(sentences[i]);
            if (newTokens + tokens <= targetOverlapTokens || retained.Count == 0)
            {
                retained.Insert(0, sentences[i]);
                newTokens += tokens;
            }
            else
            {
                break;
            }
        }

        return retained;
    }

    public static int EstimateTokenCount(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        // Accurate heuristic: approx 1 token per 3.8 - 4 characters for English / code prose
        // Or ~ 1.3 tokens per word
        var wordCount = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        var charCountTokens = (int)Math.Ceiling(text.Length / 3.8);
        var wordCountTokens = (int)Math.Ceiling(wordCount * 1.3);

        return Math.Max(1, (charCountTokens + wordCountTokens) / 2);
    }
}

namespace CaseInterview.Infrastructure.AI;

public class AiSettings
{
    public const string SectionName = "Ai";

    public string Provider { get; set; } = "OpenAI"; // "OpenAI", "AzureOpenAI", "Ollama", "Mock"
    public string ApiKey { get; set; } = "mock-key";
    public string Endpoint { get; set; } = "https://api.openai.com/v1";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public string ChatModel { get; set; } = "gpt-4o";
    public int EmbeddingDimensions { get; set; } = 1536;
}

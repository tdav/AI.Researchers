namespace AiResearchers.Infrastructure.Llm;

public class LlmOptions
{
    public const string SectionName = "Llm";

    public string Endpoint { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "bjoernb/gemma4-e2b-fast:latest";
    public float Temperature { get; set; } = 0.2f;
}

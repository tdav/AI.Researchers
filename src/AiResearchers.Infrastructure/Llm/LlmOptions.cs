namespace AiResearchers.Infrastructure.Llm;

public class LlmOptions
{
    public const string SectionName = "Llm";

    public string Endpoint { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "bjoernb/gemma4-e2b-fast:latest";
    public float Temperature { get; set; } = 0.2f;

    // Local models can be slow on cold start; the default HttpClient timeout (100s)
    // caused intermittent failures in the LLM gate. See docs/superpowers/notes/2026-05-22-llm-gate-results.md.
    public int TimeoutSeconds { get; set; } = 300;
}

namespace AiResearchers.Infrastructure.Research;

public class SearchOptions
{
    public const string SectionName = "Search";

    // "Searxng" | "DuckDuckGo"
    public string Provider { get; set; } = "Searxng";
    public string SearxngEndpoint { get; set; } = "http://localhost:8888";
}

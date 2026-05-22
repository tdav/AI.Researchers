namespace AiResearchers.Core.Research;

public class FetchedContent
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}

namespace AiResearchers.Core.Entities;

public class Report
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ResearchTaskId { get; set; }
    public string MarkdownContent { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    public ResearchTask? ResearchTask { get; set; }
}

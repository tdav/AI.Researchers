using AiResearchers.Core.Enums;

namespace AiResearchers.Core.Entities;

public class Source
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ResearchTaskId { get; set; }
    public Guid? FocusAreaId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public SourceStatus Status { get; set; } = SourceStatus.Found;
    public string? ExtractedText { get; set; }
    public DateTimeOffset? FetchedAt { get; set; }

    public ResearchTask? ResearchTask { get; set; }
    public FocusArea? FocusArea { get; set; }
    public List<Finding> Findings { get; set; } = new();
}

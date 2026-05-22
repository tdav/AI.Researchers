namespace AiResearchers.Core.Entities;

public class Finding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ResearchTaskId { get; set; }
    public Guid? OutlineSectionId { get; set; }
    public Guid SourceId { get; set; }
    public string Text { get; set; } = string.Empty;
    public int Round { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ResearchTask? ResearchTask { get; set; }
    public OutlineSection? OutlineSection { get; set; }
    public Source? Source { get; set; }
}

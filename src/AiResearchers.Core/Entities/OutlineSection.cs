namespace AiResearchers.Core.Entities;

public class OutlineSection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ResearchTaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Order { get; set; }

    public ResearchTask? ResearchTask { get; set; }
    public List<Finding> Findings { get; set; } = new();
}

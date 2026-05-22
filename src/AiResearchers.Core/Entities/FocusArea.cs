using AiResearchers.Core.Enums;

namespace AiResearchers.Core.Entities;

public class FocusArea
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ResearchTaskId { get; set; }
    public int Round { get; set; }
    public string Title { get; set; } = string.Empty;
    public FocusAreaStatus Status { get; set; } = FocusAreaStatus.Pending;

    public ResearchTask? ResearchTask { get; set; }
    public List<Source> Sources { get; set; } = new();
}

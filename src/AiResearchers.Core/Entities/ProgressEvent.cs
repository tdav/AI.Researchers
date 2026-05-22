using AiResearchers.Core.Enums;

namespace AiResearchers.Core.Entities;

public class ProgressEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ResearchTaskId { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public ProgressPhase Phase { get; set; }
    public EventLevel Level { get; set; } = EventLevel.Info;
    public string Message { get; set; } = string.Empty;

    public ResearchTask? ResearchTask { get; set; }
}

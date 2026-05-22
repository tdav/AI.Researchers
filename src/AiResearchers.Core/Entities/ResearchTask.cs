using AiResearchers.Core.Enums;

namespace AiResearchers.Core.Entities;

public class ResearchTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Topic { get; set; } = string.Empty;
    public ResearchStatus Status { get; set; } = ResearchStatus.Draft;
    public ResearchDepth Depth { get; set; } = ResearchDepth.Standard;
    public string Language { get; set; } = "ru";
    public string? Audience { get; set; }
    public int CurrentRound { get; set; }
    public int MaxRounds { get; set; } = 3;
    public string? FailureReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public List<InterviewAnswer> InterviewAnswers { get; set; } = new();
    public List<OutlineSection> OutlineSections { get; set; } = new();
    public List<FocusArea> FocusAreas { get; set; } = new();
    public List<Source> Sources { get; set; } = new();
    public List<Finding> Findings { get; set; } = new();
    public List<ProgressEvent> ProgressEvents { get; set; } = new();
    public Report? Report { get; set; }
}

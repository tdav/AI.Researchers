namespace AiResearchers.Core.Interview;

public class InterviewRound
{
    public List<ClarifyingQuestion> Questions { get; set; } = new();
    public bool EnoughRecommended { get; set; }
    public string? Rationale { get; set; }
}

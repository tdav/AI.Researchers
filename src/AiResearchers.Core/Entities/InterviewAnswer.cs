using AiResearchers.Core.Enums;

namespace AiResearchers.Core.Entities;

public class InterviewAnswer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ResearchTaskId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public AnswerSource Source { get; set; }
    public int Order { get; set; }

    public ResearchTask? ResearchTask { get; set; }
}

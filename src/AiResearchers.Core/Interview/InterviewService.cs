using System.Text;
using AiResearchers.Core.Entities;
using AiResearchers.Core.Enums;
using Microsoft.Extensions.AI;

namespace AiResearchers.Core.Interview;

public class InterviewService : IInterviewService
{
    private readonly IChatClient chatClient;

    public InterviewService(IChatClient chatClient)
    {
        this.chatClient = chatClient;
    }

    public async Task<InterviewRound> GenerateClarifyingRoundAsync(
        ResearchTask task, int roundNumber, int maxRounds, CancellationToken cancellationToken = default)
    {
        bool isLastAllowedRound = roundNumber >= maxRounds;
        string prompt =
            $"You are an expert research interviewer scoping a task before any web research begins. " +
            $"Topic: \"{task.Topic}\". Depth: {task.Depth}. Audience: {task.Audience ?? "general"}. " +
            $"{this.RenderAnswers(task)}" +
            $"This is clarification round {roundNumber} of at most {maxRounds}. " +
            $"Think about what is still unknown: identify the coverage gaps that would most change the " +
            $"research direction, and prioritize the highest-impact clarifications. " +
            $"Do NOT repeat anything already answered above. " +
            $"Generate 2-4 clarifying questions. For EACH question: " +
            $"set \"kind\" to \"single\" when exactly one answer fits, or \"multi\" when several may apply; " +
            $"provide 2-5 concrete, mutually distinct \"options\" the user can pick from. " +
            $"Set \"enoughRecommended\" to true if the answers already cover the scope well enough to start. " +
            (isLastAllowedRound ? "This is the final allowed round, so set \"enoughRecommended\" to true. " : string.Empty) +
            $"Optionally give a one-sentence \"rationale\" for your recommendation. " +
            $"All question text, options and rationale must be in this language: {task.Language}. " +
            $"Return JSON: {{ \"questions\": [{{ \"text\": \"...\", \"kind\": \"single|multi\", " +
            $"\"options\": [\"...\"] }}], \"enoughRecommended\": false, \"rationale\": \"...\" }}.";

        ChatResponse<InterviewRoundDto> response =
            await this.chatClient.GetResponseAsync<InterviewRoundDto>(prompt, cancellationToken: cancellationToken);

        InterviewRoundDto dto = response.Result ?? new InterviewRoundDto();
        return new InterviewRound
        {
            EnoughRecommended = dto.EnoughRecommended || isLastAllowedRound,
            Rationale = string.IsNullOrWhiteSpace(dto.Rationale) ? null : dto.Rationale,
            Questions = dto.Questions
                .Where(q => !string.IsNullOrWhiteSpace(q.Text))
                .Select(q => new ClarifyingQuestion
                {
                    Text = q.Text,
                    Kind = string.Equals(q.Kind, "multi", StringComparison.OrdinalIgnoreCase)
                        ? QuestionKind.Multi : QuestionKind.Single,
                    Options = (q.Options ?? new List<string>())
                        .Where(o => !string.IsNullOrWhiteSpace(o)).ToList(),
                    AllowOther = true
                })
                .ToList()
        };
    }

    public async Task<IReadOnlyList<OutlineDraftSection>> GenerateOutlineDraftAsync(
        ResearchTask task, CancellationToken cancellationToken = default)
    {
        string prompt =
            $"Design the structure of a research report. Topic: \"{task.Topic}\". " +
            $"Depth: {task.Depth}. Audience: {task.Audience ?? "general"}. " +
            $"{this.RenderAnswers(task)}" +
            $"Propose 4-7 report sections, each with a short title and a one-sentence description of what it covers. " +
            $"Section titles and descriptions language: {task.Language}. " +
            $"Return JSON: {{ \"sections\": [{{ \"title\": \"...\", \"description\": \"...\" }}] }}.";

        ChatResponse<OutlineDraft> response =
            await this.chatClient.GetResponseAsync<OutlineDraft>(prompt, cancellationToken: cancellationToken);

        return response.Result?.Sections ?? new List<OutlineDraftSection>();
    }

    private string RenderAnswers(ResearchTask task)
    {
        if (task.InterviewAnswers.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder sb = new("Known answers so far:\n");
        foreach (InterviewAnswer a in task.InterviewAnswers)
        {
            sb.Append("- Q: ").Append(a.Question).Append(" A: ").Append(a.Answer).Append('\n');
        }
        return sb.ToString();
    }

    private sealed class InterviewRoundDto
    {
        public List<ClarifyingQuestionDto> Questions { get; set; } = new();
        public bool EnoughRecommended { get; set; }
        public string? Rationale { get; set; }
    }

    private sealed class ClarifyingQuestionDto
    {
        public string Text { get; set; } = string.Empty;
        public string Kind { get; set; } = "single";
        public List<string>? Options { get; set; }
    }

    private sealed class OutlineDraft
    {
        public List<OutlineDraftSection> Sections { get; set; } = new();
    }
}

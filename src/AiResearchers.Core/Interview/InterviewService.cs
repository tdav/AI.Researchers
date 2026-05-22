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

    public async Task<IReadOnlyList<string>> GenerateFollowUpQuestionsAsync(
        ResearchTask task, CancellationToken cancellationToken = default)
    {
        string prompt =
            $"You are scoping a research task. Topic: \"{task.Topic}\". " +
            $"Depth: {task.Depth}. Audience: {task.Audience ?? "general"}. " +
            $"{this.RenderAnswers(task)}" +
            $"Generate 3-5 concise clarifying questions that would most improve the research direction. " +
            $"Answer language: {task.Language}. " +
            $"Return JSON: {{ \"questions\": [\"...\"] }}.";

        ChatResponse<FollowUpQuestions> response =
            await this.chatClient.GetResponseAsync<FollowUpQuestions>(prompt, cancellationToken: cancellationToken);

        return response.Result?.Questions ?? new List<string>();
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

    private sealed class FollowUpQuestions
    {
        public List<string> Questions { get; set; } = new();
    }

    private sealed class OutlineDraft
    {
        public List<OutlineDraftSection> Sections { get; set; } = new();
    }
}

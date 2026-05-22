using System.Text;
using AiResearchers.Core.Entities;
using Microsoft.Extensions.AI;

namespace AiResearchers.Core.Agents;

public class PlannerAgent : IPlannerAgent
{
    private readonly IChatClient chatClient;

    public PlannerAgent(IChatClient chatClient)
    {
        this.chatClient = chatClient;
    }

    public async Task<IReadOnlyList<string>> PlanFocusAreasAsync(
        ResearchTask task, CancellationToken cancellationToken = default)
    {
        StringBuilder outline = new();
        foreach (OutlineSection s in task.OutlineSections.OrderBy(s => s.Order))
        {
            outline.Append("- ").Append(s.Title).Append(": ").Append(s.Description).Append('\n');
        }

        string prompt =
            $"You plan web research. Topic: \"{task.Topic}\". Report outline:\n{outline}" +
            $"List 4-6 distinct focus areas (search angles) that together cover the outline. " +
            $"Language: {task.Language}. Return JSON: {{ \"focusAreas\": [\"...\"] }}.";

        FocusAreasResult? result =
            await LlmRetry.GetJsonAsync<FocusAreasResult>(this.chatClient, prompt, cancellationToken);
        return result?.FocusAreas ?? new List<string>();
    }
}

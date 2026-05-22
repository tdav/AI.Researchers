using System.Text;
using AiResearchers.Core.Entities;
using Microsoft.Extensions.AI;

namespace AiResearchers.Core.Agents;

public class CriticAgent : ICriticAgent
{
    private readonly IChatClient chatClient;

    public CriticAgent(IChatClient chatClient)
    {
        this.chatClient = chatClient;
    }

    public async Task<CritiqueResult> CritiqueAsync(
        ResearchTask task, IReadOnlyList<string> findingsSummary,
        CancellationToken cancellationToken = default)
    {
        StringBuilder outline = new();
        foreach (OutlineSection s in task.OutlineSections.OrderBy(s => s.Order))
        {
            outline.Append("- ").Append(s.Title).Append('\n');
        }

        string summary = findingsSummary.Count == 0
            ? "(no findings yet)"
            : string.Join("\n", findingsSummary.Take(40).Select(f => "- " + f));

        string prompt =
            $"Topic: \"{task.Topic}\". Report outline:\n{outline}" +
            $"Findings collected so far:\n{summary}\n" +
            $"Decide if coverage is sufficient for all sections. " +
            $"If not, propose up to 3 new focus areas to fill the biggest gaps. " +
            $"Return JSON: {{ \"enough\": true|false, \"newFocusAreas\": [\"...\"] }}.";

        CritiqueResult? result =
            await LlmRetry.GetJsonAsync<CritiqueResult>(this.chatClient, prompt, cancellationToken);
        return result ?? new CritiqueResult { Enough = true };
    }
}

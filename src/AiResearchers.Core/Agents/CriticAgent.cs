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

    private const int MaxFindingsPerSection = 6;

    public async Task<CritiqueResult> CritiqueAsync(
        ResearchTask task, IReadOnlyList<FindingNote> findings,
        CancellationToken cancellationToken = default)
    {
        // Group findings under the outline section titles so the model assesses
        // coverage per section rather than over one flat list. Notes whose tagged
        // section doesn't match an outline title fall into "(unassigned)".
        List<string> sectionTitles = task.OutlineSections
            .OrderBy(s => s.Order).Select(s => s.Title).ToList();

        StringBuilder grouped = new();
        foreach (string title in sectionTitles)
        {
            List<FindingNote> matched = findings
                .Where(f => string.Equals(f.Section, title, StringComparison.OrdinalIgnoreCase))
                .ToList();
            grouped.Append("\n### ").Append(title).Append(" (").Append(matched.Count).Append(" findings)\n");
            foreach (FindingNote f in matched.Take(MaxFindingsPerSection))
            {
                grouped.Append("- ").Append(f.Text).Append('\n');
            }
        }

        List<FindingNote> unassigned = findings
            .Where(f => !sectionTitles.Any(t => string.Equals(f.Section, t, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (unassigned.Count > 0)
        {
            grouped.Append("\n### (unassigned) (").Append(unassigned.Count).Append(" findings)\n");
            foreach (FindingNote f in unassigned.Take(MaxFindingsPerSection))
            {
                grouped.Append("- ").Append(f.Text).Append('\n');
            }
        }

        string body = findings.Count == 0 ? "(no findings yet)" : grouped.ToString();

        string prompt =
            $"Topic: \"{task.Topic}\". You are the research critic.\n" +
            $"Findings grouped by report section:\n{body}\n\n" +
            $"For EACH section decide if its findings are sufficient and rate confidence 0-100. " +
            $"Flag any findings that contradict each other across sources. " +
            $"Decide if overall coverage is sufficient for all sections. " +
            $"If not, propose up to 3 new focus areas targeting the weakest sections. " +
            $"Return JSON: {{ \"enough\": true|false, \"newFocusAreas\": [\"...\"], " +
            $"\"coverage\": [{{ \"section\": \"...\", \"covered\": true|false, \"confidence\": 0 }}], " +
            $"\"conflicts\": [\"...\"] }}.";

        CritiqueResult? result =
            await LlmRetry.GetJsonAsync<CritiqueResult>(this.chatClient, prompt, cancellationToken);
        return result ?? new CritiqueResult { Enough = true };
    }
}

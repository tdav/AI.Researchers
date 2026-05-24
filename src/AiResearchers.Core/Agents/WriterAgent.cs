using System.Text;
using AiResearchers.Core.Entities;
using Microsoft.Extensions.AI;

namespace AiResearchers.Core.Agents;

public class WriterAgent : IWriterAgent
{
    private readonly IChatClient chatClient;

    public WriterAgent(IChatClient chatClient)
    {
        this.chatClient = chatClient;
    }

    // Writer возвращает Markdown как plain text (не JSON).
    public async Task<string> WriteReportAsync(
        ResearchTask task, CancellationToken cancellationToken = default)
    {
        // Assign each cited source a stable [n] number in order of first appearance,
        // walking findings in the same order they are emitted to the model below.
        var sourceNumber = new Dictionary<Guid, int>();

        StringBuilder context = new();
        context.Append("Outline and findings (each fact ends with citation markers like [1]):\n");
        foreach (OutlineSection s in task.OutlineSections.OrderBy(s => s.Order))
        {
            context.Append("\n## ").Append(s.Title).Append(" — ").Append(s.Description).Append('\n');
            AppendFindings(context, task.Findings.Where(f => f.OutlineSectionId == s.Id), sourceNumber);
        }

        List<Finding> unassigned = task.Findings.Where(f => f.OutlineSectionId is null).ToList();
        if (unassigned.Count > 0)
        {
            context.Append("\nAdditional findings:\n");
            AppendFindings(context, unassigned, sourceNumber);
        }

        string prompt =
            $"Write a research report in Markdown. Topic: \"{task.Topic}\". Language: {task.Language}. " +
            $"Use the outline section titles as `##` headings, in order. " +
            $"Base the content ONLY on the findings provided; if a section has no findings, note that data was insufficient. " +
            $"When you state a fact, keep its citation marker(s) (e.g. [1], [2][3]) inline right after the sentence. " +
            $"Do not invent or renumber citations, and do NOT write a sources/references list — it is appended automatically. " +
            $"Keep it well-structured.\n\n{context}";

        ChatResponse response =
            await this.chatClient.GetResponseAsync(prompt, cancellationToken: cancellationToken);

        return response.Text + BuildReferences(task, sourceNumber);
    }

    // Emits one bullet per unique fact (near-duplicates merged), with the citation
    // numbers of every source that reported it — so corroborated facts carry [1][2].
    private static void AppendFindings(
        StringBuilder context, IEnumerable<Finding> findings, Dictionary<Guid, int> sourceNumber)
    {
        var byFact = new Dictionary<string, (string Text, SortedSet<int> Cites)>();
        var order = new List<string>();

        foreach (Finding f in findings)
        {
            string key = FindingDedup.Normalize(f.Text);
            if (key.Length == 0)
            {
                continue;
            }
            if (!byFact.TryGetValue(key, out (string Text, SortedSet<int> Cites) entry))
            {
                entry = (f.Text, new SortedSet<int>());
                byFact[key] = entry;
                order.Add(key);
            }
            if (f.Source is not null)
            {
                entry.Cites.Add(NumberFor(f.Source, sourceNumber));
            }
        }

        foreach (string key in order)
        {
            (string Text, SortedSet<int> Cites) entry = byFact[key];
            context.Append("- ").Append(entry.Text);
            foreach (int n in entry.Cites)
            {
                context.Append('[').Append(n).Append(']');
            }
            context.Append('\n');
        }
    }

    private static int NumberFor(Source source, Dictionary<Guid, int> sourceNumber)
    {
        if (!sourceNumber.TryGetValue(source.Id, out int n))
        {
            n = sourceNumber.Count + 1;
            sourceNumber[source.Id] = n;
        }
        return n;
    }

    private static string BuildReferences(ResearchTask task, Dictionary<Guid, int> sourceNumber)
    {
        if (sourceNumber.Count == 0)
        {
            return string.Empty;
        }

        Dictionary<Guid, Source> sources = task.Findings
            .Where(f => f.Source is not null)
            .Select(f => f.Source!)
            .GroupBy(s => s.Id)
            .ToDictionary(g => g.Key, g => g.First());

        StringBuilder refs = new();
        refs.Append("\n\n## Источники\n\n");
        foreach (KeyValuePair<Guid, int> kv in sourceNumber.OrderBy(kv => kv.Value))
        {
            if (!sources.TryGetValue(kv.Key, out Source? s))
            {
                continue;
            }
            string title = string.IsNullOrWhiteSpace(s.Title) ? s.Url : s.Title;
            refs.Append('[').Append(kv.Value).Append("] ").Append(title).Append(" — ").Append(s.Url).Append('\n');
        }
        return refs.ToString();
    }
}

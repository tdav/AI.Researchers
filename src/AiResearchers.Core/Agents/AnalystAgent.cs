using System.Text;
using AiResearchers.Core.Entities;
using Microsoft.Extensions.AI;

namespace AiResearchers.Core.Agents;

public class AnalystAgent : IAnalystAgent
{
    private const int MaxSourceChars = 6000;

    // Long pages are read in up to this many chunks instead of being truncated to
    // the first window. Each extra chunk is one more LLM call, so the cap trades
    // recall against per-page latency on a slow local model — keep it small.
    private const int MaxChunksPerSource = 2;
    private const int MaxFindingsPerSource = 8;

    private readonly IChatClient chatClient;

    public AnalystAgent(IChatClient chatClient)
    {
        this.chatClient = chatClient;
    }

    public async Task<IReadOnlyList<FindingItem>> ExtractFindingsAsync(
        ResearchTask task, string focusArea, string sourceText,
        CancellationToken cancellationToken = default)
    {
        StringBuilder sections = new();
        foreach (OutlineSection s in task.OutlineSections.OrderBy(s => s.Order))
        {
            sections.Append("- ").Append(s.Title).Append('\n');
        }

        var collected = new List<FindingItem>();
        var seen = new HashSet<string>();

        foreach (string chunk in Chunk(sourceText))
        {
            string prompt =
                $"Topic: \"{task.Topic}\". Focus area: \"{focusArea}\". Report sections:\n{sections}" +
                $"From the SOURCE TEXT below, extract 0-5 concrete factual findings relevant to the topic. " +
                $"For each, optionally name the most relevant section from the list above. " +
                $"Ignore navigation/boilerplate. Language: {task.Language}. " +
                $"Return JSON: {{ \"findings\": [{{ \"text\": \"...\", \"sectionTitle\": \"...\" }}] }}.\n\n" +
                $"SOURCE TEXT:\n{chunk}";

            FindingsResult? result =
                await LlmRetry.GetJsonAsync<FindingsResult>(this.chatClient, prompt, cancellationToken);
            if (result?.Findings is null)
            {
                continue;
            }

            foreach (FindingItem fi in result.Findings)
            {
                string key = FindingDedup.Normalize(fi.Text);
                if (key.Length == 0 || !seen.Add(key))
                {
                    continue;
                }
                collected.Add(fi);
                if (collected.Count >= MaxFindingsPerSource)
                {
                    return collected;
                }
            }
        }

        return collected;
    }

    private static IEnumerable<string> Chunk(string text)
    {
        if (text.Length <= MaxSourceChars)
        {
            yield return text;
            yield break;
        }

        for (int i = 0, n = 0; i < text.Length && n < MaxChunksPerSource; i += MaxSourceChars, n++)
        {
            int len = Math.Min(MaxSourceChars, text.Length - i);
            yield return text.Substring(i, len);
        }
    }
}

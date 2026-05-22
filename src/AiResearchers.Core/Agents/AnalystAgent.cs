using System.Text;
using AiResearchers.Core.Entities;
using Microsoft.Extensions.AI;

namespace AiResearchers.Core.Agents;

public class AnalystAgent : IAnalystAgent
{
    private const int MaxSourceChars = 6000;
    private readonly IChatClient chatClient;

    public AnalystAgent(IChatClient chatClient)
    {
        this.chatClient = chatClient;
    }

    public async Task<IReadOnlyList<FindingItem>> ExtractFindingsAsync(
        ResearchTask task, string focusArea, string sourceText,
        CancellationToken cancellationToken = default)
    {
        string trimmed = sourceText.Length > MaxSourceChars
            ? sourceText[..MaxSourceChars]
            : sourceText;

        StringBuilder sections = new();
        foreach (OutlineSection s in task.OutlineSections.OrderBy(s => s.Order))
        {
            sections.Append("- ").Append(s.Title).Append('\n');
        }

        string prompt =
            $"Topic: \"{task.Topic}\". Focus area: \"{focusArea}\". Report sections:\n{sections}" +
            $"From the SOURCE TEXT below, extract 0-5 concrete factual findings relevant to the topic. " +
            $"For each, optionally name the most relevant section from the list above. " +
            $"Ignore navigation/boilerplate. Language: {task.Language}. " +
            $"Return JSON: {{ \"findings\": [{{ \"text\": \"...\", \"sectionTitle\": \"...\" }}] }}.\n\n" +
            $"SOURCE TEXT:\n{trimmed}";

        FindingsResult? result =
            await LlmRetry.GetJsonAsync<FindingsResult>(this.chatClient, prompt, cancellationToken);
        return result?.Findings ?? new List<FindingItem>();
    }
}

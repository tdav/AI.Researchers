using AiResearchers.Core.Entities;
using Microsoft.Extensions.AI;

namespace AiResearchers.Core.Agents;

public class SearcherAgent : ISearcherAgent
{
    private readonly IChatClient chatClient;

    public SearcherAgent(IChatClient chatClient)
    {
        this.chatClient = chatClient;
    }

    public async Task<IReadOnlyList<string>> GenerateQueriesAsync(
        ResearchTask task, string focusArea, CancellationToken cancellationToken = default)
    {
        string prompt =
            $"Topic: \"{task.Topic}\". Focus area to research (a gap to fill): \"{focusArea}\". " +
            $"Generate 2-3 effective web search queries (plain keywords, no operators) that approach this " +
            $"focus area from different angles. Make at least one query specific (concrete names, terms, " +
            $"numbers) rather than generic. If the focus area is time-sensitive, include the year " +
            $"{DateTimeOffset.UtcNow.Year} in one query. Avoid near-duplicate queries. " +
            $"Return JSON: {{ \"queries\": [\"...\"] }}.";

        SearchQueriesResult? result =
            await LlmRetry.GetJsonAsync<SearchQueriesResult>(this.chatClient, prompt, cancellationToken);
        return result?.Queries ?? new List<string> { focusArea };
    }
}

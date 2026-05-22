using AiResearchers.Core.Entities;

namespace AiResearchers.Core.Agents;

public interface ISearcherAgent
{
    Task<IReadOnlyList<string>> GenerateQueriesAsync(
        ResearchTask task, string focusArea, CancellationToken cancellationToken = default);
}

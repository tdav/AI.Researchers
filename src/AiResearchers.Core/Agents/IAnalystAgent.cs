using AiResearchers.Core.Entities;

namespace AiResearchers.Core.Agents;

public interface IAnalystAgent
{
    Task<IReadOnlyList<FindingItem>> ExtractFindingsAsync(
        ResearchTask task, string focusArea, string sourceText,
        CancellationToken cancellationToken = default);
}

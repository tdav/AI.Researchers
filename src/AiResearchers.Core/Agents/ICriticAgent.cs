using AiResearchers.Core.Entities;

namespace AiResearchers.Core.Agents;

public interface ICriticAgent
{
    Task<CritiqueResult> CritiqueAsync(
        ResearchTask task, IReadOnlyList<FindingNote> findings,
        CancellationToken cancellationToken = default);
}

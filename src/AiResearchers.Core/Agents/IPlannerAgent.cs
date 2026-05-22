using AiResearchers.Core.Entities;

namespace AiResearchers.Core.Agents;

public interface IPlannerAgent
{
    Task<IReadOnlyList<string>> PlanFocusAreasAsync(
        ResearchTask task, CancellationToken cancellationToken = default);
}

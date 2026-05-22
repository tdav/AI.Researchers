using AiResearchers.Core.Entities;

namespace AiResearchers.Core.Interview;

public interface IInterviewService
{
    Task<InterviewRound> GenerateClarifyingRoundAsync(
        ResearchTask task, int roundNumber, int maxRounds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OutlineDraftSection>> GenerateOutlineDraftAsync(
        ResearchTask task, CancellationToken cancellationToken = default);
}

using AiResearchers.Core.Entities;

namespace AiResearchers.Core.Interview;

public interface IInterviewService
{
    Task<IReadOnlyList<string>> GenerateFollowUpQuestionsAsync(
        ResearchTask task, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OutlineDraftSection>> GenerateOutlineDraftAsync(
        ResearchTask task, CancellationToken cancellationToken = default);
}

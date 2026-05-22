namespace AiResearchers.Core.Orchestration;

public interface IResearchOrchestrator
{
    Task RunAsync(Guid researchTaskId, CancellationToken cancellationToken);
}

namespace AiResearchers.Core.Orchestration;

public interface IResearchQueue
{
    void Enqueue(Guid researchTaskId);
    IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken);
}

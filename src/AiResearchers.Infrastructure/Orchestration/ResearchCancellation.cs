using System.Collections.Concurrent;
using AiResearchers.Core.Orchestration;

namespace AiResearchers.Infrastructure.Orchestration;

public class ResearchCancellation : IResearchCancellation
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> running = new();

    public CancellationTokenSource Register(Guid researchTaskId, CancellationToken linkedTo)
    {
        CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(linkedTo);
        this.running[researchTaskId] = cts;
        return cts;
    }

    public void Complete(Guid researchTaskId)
    {
        if (this.running.TryRemove(researchTaskId, out CancellationTokenSource? cts))
        {
            cts.Dispose();
        }
    }

    public bool Cancel(Guid researchTaskId)
    {
        if (this.running.TryGetValue(researchTaskId, out CancellationTokenSource? cts))
        {
            cts.Cancel();
            return true;
        }
        return false;
    }
}

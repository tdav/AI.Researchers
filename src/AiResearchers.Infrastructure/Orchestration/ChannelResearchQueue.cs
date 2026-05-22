using System.Threading.Channels;
using AiResearchers.Core.Orchestration;

namespace AiResearchers.Infrastructure.Orchestration;

public class ChannelResearchQueue : IResearchQueue
{
    private readonly Channel<Guid> channel =
        Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(Guid researchTaskId)
    {
        this.channel.Writer.TryWrite(researchTaskId);
    }

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken)
    {
        return this.channel.Reader.ReadAllAsync(cancellationToken);
    }
}

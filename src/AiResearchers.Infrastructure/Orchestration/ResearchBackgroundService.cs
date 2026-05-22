using AiResearchers.Core.Orchestration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiResearchers.Infrastructure.Orchestration;

public class ResearchBackgroundService : BackgroundService
{
    private readonly IResearchQueue queue;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<ResearchBackgroundService> logger;

    public ResearchBackgroundService(
        IResearchQueue queue, IServiceScopeFactory scopeFactory, ILogger<ResearchBackgroundService> logger)
    {
        this.queue = queue;
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (Guid id in this.queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                using IServiceScope scope = this.scopeFactory.CreateScope();
                IResearchOrchestrator orchestrator =
                    scope.ServiceProvider.GetRequiredService<IResearchOrchestrator>();
                await orchestrator.RunAsync(id, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Worker failed processing {Id}", id);
            }
        }
    }
}

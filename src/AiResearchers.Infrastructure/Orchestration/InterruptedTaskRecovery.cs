using AiResearchers.Core.Enums;
using AiResearchers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiResearchers.Infrastructure.Orchestration;

// При старте приложения: задачи, оставшиеся Running после прошлого падения/рестарта, → Failed.
public class InterruptedTaskRecovery : IHostedService
{
    private readonly IServiceScopeFactory scopeFactory;

    public InterruptedTaskRecovery(IServiceScopeFactory scopeFactory)
    {
        this.scopeFactory = scopeFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = this.scopeFactory.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.ResearchTasks
            .Where(t => t.Status == ResearchStatus.Running)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, ResearchStatus.Failed)
                .SetProperty(t => t.FailureReason, "interrupted by restart"), cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

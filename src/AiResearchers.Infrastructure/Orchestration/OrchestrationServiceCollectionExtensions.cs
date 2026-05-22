using AiResearchers.Core.Orchestration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiResearchers.Infrastructure.Orchestration;

public static class OrchestrationServiceCollectionExtensions
{
    public static IServiceCollection AddOrchestration(this IServiceCollection services)
    {
        services.AddSingleton<IResearchQueue, ChannelResearchQueue>();
        services.AddScoped<IResearchOrchestrator, ResearchOrchestrator>();
        services.AddHostedService<ResearchBackgroundService>();
        services.AddHostedService<InterruptedTaskRecovery>();
        return services;
    }
}

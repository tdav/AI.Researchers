using Microsoft.Extensions.DependencyInjection;

namespace AiResearchers.Core.Agents;

public static class AgentsServiceCollectionExtensions
{
    public static IServiceCollection AddAgents(this IServiceCollection services)
    {
        services.AddScoped<IPlannerAgent, PlannerAgent>();
        services.AddScoped<ISearcherAgent, SearcherAgent>();
        services.AddScoped<IAnalystAgent, AnalystAgent>();
        services.AddScoped<ICriticAgent, CriticAgent>();
        services.AddScoped<IWriterAgent, WriterAgent>();
        return services;
    }
}

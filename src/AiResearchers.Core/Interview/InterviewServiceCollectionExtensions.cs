using Microsoft.Extensions.DependencyInjection;

namespace AiResearchers.Core.Interview;

public static class InterviewServiceCollectionExtensions
{
    public static IServiceCollection AddInterview(this IServiceCollection services)
    {
        services.AddScoped<IInterviewService, InterviewService>();
        return services;
    }
}

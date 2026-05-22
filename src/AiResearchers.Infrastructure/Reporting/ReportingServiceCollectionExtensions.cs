using AiResearchers.Core.Reporting;
using Microsoft.Extensions.DependencyInjection;

namespace AiResearchers.Infrastructure.Reporting;

public static class ReportingServiceCollectionExtensions
{
    public static IServiceCollection AddReporting(this IServiceCollection services)
    {
        services.AddSingleton<IReportExporter, ReportExporter>();
        return services;
    }
}

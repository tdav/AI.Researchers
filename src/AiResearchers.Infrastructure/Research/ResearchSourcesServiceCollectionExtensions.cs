using System.Net.Http.Headers;
using AiResearchers.Core.Research;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiResearchers.Infrastructure.Research;

public static class ResearchSourcesServiceCollectionExtensions
{
    private const string UserAgent =
        "Mozilla/5.0 (compatible; AiResearchers/1.0; +local)";

    public static IServiceCollection AddResearchSources(
        this IServiceCollection services, IConfiguration configuration)
    {
        SearchOptions options = configuration.GetSection(SearchOptions.SectionName).Get<SearchOptions>()
            ?? new SearchOptions();
        services.Configure<SearchOptions>(configuration.GetSection(SearchOptions.SectionName));

        // Named HttpClient для поиска и фетча: общий UA и таймаут.
        services.AddHttpClient("research", c =>
        {
            c.Timeout = TimeSpan.FromSeconds(30);
            c.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        });

        if (string.Equals(options.Provider, "DuckDuckGo", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<ISearchProvider>(sp =>
                new DuckDuckGoSearchProvider(
                    sp.GetRequiredService<IHttpClientFactory>().CreateClient("research")));
        }
        else
        {
            services.AddSingleton<ISearchProvider>(sp =>
                new SearxngSearchProvider(
                    sp.GetRequiredService<IHttpClientFactory>().CreateClient("research"),
                    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SearchOptions>>()));
        }

        services.AddSingleton<IContentFetcher>(sp =>
            new HtmlContentFetcher(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("research")));

        return services;
    }
}

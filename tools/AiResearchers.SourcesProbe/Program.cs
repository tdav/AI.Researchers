using AiResearchers.Core.Research;
using AiResearchers.Infrastructure.Research;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        string query = args.Length > 0 ? string.Join(' ', args) : "local LLM private RAG 2026";

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Search:Provider"] = Environment.GetEnvironmentVariable("SEARCH_PROVIDER") ?? "Searxng",
                ["Search:SearxngEndpoint"] = "http://localhost:8888"
            })
            .Build();

        ServiceProvider services = new ServiceCollection()
            .AddResearchSources(config)
            .BuildServiceProvider();

        ISearchProvider search = services.GetRequiredService<ISearchProvider>();
        IContentFetcher fetcher = services.GetRequiredService<IContentFetcher>();

        Console.WriteLine($"Search: \"{query}\" via {config["Search:Provider"]}");
        IReadOnlyList<SearchResultItem> results = await search.SearchAsync(query, 5);
        Console.WriteLine($"  {results.Count} results");
        foreach (SearchResultItem r in results.Take(5))
        {
            Console.WriteLine($"   - {r.Title} :: {r.Url}");
        }

        if (results.Count == 0)
        {
            Console.WriteLine("NO RESULTS — check search provider/container.");
            return 1;
        }

        // Iterate up to 3 results until one fetch yields readable content.
        // The top result may be a bot-walled page (e.g. Reddit) with no extractable text.
        FetchedContent? successful = null;
        foreach (SearchResultItem r in results.Take(3))
        {
            Console.WriteLine($"\nFetch: {r.Url}");
            FetchedContent content = await fetcher.FetchAsync(r.Url);
            Console.WriteLine($"  success={content.Success} title=\"{content.Title}\" textLen={content.Text.Length} error={content.Error}");
            if (content.Success)
            {
                successful = content;
                break;
            }
        }

        return successful is not null ? 0 : 2;
    }
}
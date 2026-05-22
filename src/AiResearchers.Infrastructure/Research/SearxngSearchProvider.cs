using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AiResearchers.Core.Research;
using Microsoft.Extensions.Options;

namespace AiResearchers.Infrastructure.Research;

public class SearxngSearchProvider : ISearchProvider
{
    private readonly HttpClient httpClient;
    private readonly SearchOptions options;

    public SearxngSearchProvider(HttpClient httpClient, IOptions<SearchOptions> options)
    {
        this.httpClient = httpClient;
        this.options = options.Value;
    }

    public async Task<IReadOnlyList<SearchResultItem>> SearchAsync(
        string query, int maxResults = 10, CancellationToken cancellationToken = default)
    {
        string url = $"{this.options.SearxngEndpoint.TrimEnd('/')}/search" +
                     $"?q={Uri.EscapeDataString(query)}&format=json";

        SearxngResponse? response =
            await this.httpClient.GetFromJsonAsync<SearxngResponse>(url, cancellationToken);

        if (response?.Results is null)
        {
            return new List<SearchResultItem>();
        }

        return response.Results
            .Where(r => !string.IsNullOrWhiteSpace(r.Url))
            .Take(maxResults)
            .Select(r => new SearchResultItem
            {
                Url = r.Url!,
                Title = r.Title ?? string.Empty,
                Snippet = r.Content ?? string.Empty
            })
            .ToList();
    }

    private sealed class SearxngResponse
    {
        [JsonPropertyName("results")]
        public List<SearxngResult>? Results { get; set; }
    }

    private sealed class SearxngResult
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}

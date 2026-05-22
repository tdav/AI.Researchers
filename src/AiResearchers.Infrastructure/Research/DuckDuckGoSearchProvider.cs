using System.Net;
using AngleSharp.Html.Parser;
using AiResearchers.Core.Research;

namespace AiResearchers.Infrastructure.Research;

public class DuckDuckGoSearchProvider : ISearchProvider
{
    private readonly HttpClient httpClient;

    public DuckDuckGoSearchProvider(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<IReadOnlyList<SearchResultItem>> SearchAsync(
        string query, int maxResults = 10, CancellationToken cancellationToken = default)
    {
        // html.duckduckgo.com отдаёт статическую страницу результатов, пригодную для парсинга.
        string url = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
        string html = await this.httpClient.GetStringAsync(url, cancellationToken);

        HtmlParser parser = new();
        AngleSharp.Html.Dom.IHtmlDocument doc = await parser.ParseDocumentAsync(html, cancellationToken);

        List<SearchResultItem> items = new();
        foreach (AngleSharp.Dom.IElement result in doc.QuerySelectorAll("div.result"))
        {
            AngleSharp.Dom.IElement? link = result.QuerySelector("a.result__a");
            if (link is null)
            {
                continue;
            }

            string href = link.GetAttribute("href") ?? string.Empty;
            string realUrl = ExtractRealUrl(href);
            if (string.IsNullOrWhiteSpace(realUrl))
            {
                continue;
            }

            string snippet = result.QuerySelector("a.result__snippet")?.TextContent?.Trim() ?? string.Empty;
            items.Add(new SearchResultItem
            {
                Url = realUrl,
                Title = link.TextContent.Trim(),
                Snippet = snippet
            });

            if (items.Count >= maxResults)
            {
                break;
            }
        }

        return items;
    }

    // DDG оборачивает ссылки в редирект /l/?uddg=<encoded-url>. Достаём реальный URL.
    private static string ExtractRealUrl(string href)
    {
        if (href.Contains("uddg=", StringComparison.OrdinalIgnoreCase))
        {
            int idx = href.IndexOf("uddg=", StringComparison.OrdinalIgnoreCase) + "uddg=".Length;
            int amp = href.IndexOf('&', idx);
            string encoded = amp > idx ? href[idx..amp] : href[idx..];
            return WebUtility.UrlDecode(encoded);
        }
        return href.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? href : string.Empty;
    }
}

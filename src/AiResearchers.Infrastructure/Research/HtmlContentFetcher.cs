using AiResearchers.Core.Research;
using SmartReader;

namespace AiResearchers.Infrastructure.Research;

public class HtmlContentFetcher : IContentFetcher
{
    private readonly HttpClient httpClient;

    public HtmlContentFetcher(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<FetchedContent> FetchAsync(string url, CancellationToken cancellationToken = default)
    {
        FetchedContent content = new() { Url = url };

        string? html = await this.DownloadAsync(url, cancellationToken);
        if (html is null)
        {
            content.Success = false;
            content.Error = "download failed";
            return content;
        }

        try
        {
            Reader reader = new(url, html);
            Article article = reader.GetArticle();
            content.Title = article.Title ?? string.Empty;
            content.Text = (article.TextContent ?? string.Empty).Trim();
            content.Success = !string.IsNullOrWhiteSpace(content.Text);
            if (!content.Success)
            {
                content.Error = "no readable content extracted";
            }
        }
        catch (Exception ex)
        {
            content.Success = false;
            content.Error = $"extract failed: {ex.Message}";
        }

        return content;
    }

    // Скачать HTML с одним повтором на transient-сбой.
    private async Task<string?> DownloadAsync(string url, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using HttpResponseMessage response =
                    await this.httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }
                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception) when (attempt == 0 && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
        return null;
    }
}

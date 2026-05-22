namespace AiResearchers.Core.Research;

public interface IContentFetcher
{
    Task<FetchedContent> FetchAsync(string url, CancellationToken cancellationToken = default);
}

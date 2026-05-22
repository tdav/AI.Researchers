namespace AiResearchers.Core.Research;

public interface ISearchProvider
{
    Task<IReadOnlyList<SearchResultItem>> SearchAsync(
        string query, int maxResults = 10, CancellationToken cancellationToken = default);
}

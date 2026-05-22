using Microsoft.Extensions.AI;

namespace AiResearchers.Core.Agents;

// Retry с backoff для structured-output вызовов IChatClient.
// Покрывает transient HTTP-ошибки и таймауты, выявленные в LLM gate.
public static class LlmRetry
{
    public static async Task<T?> GetJsonAsync<T>(
        IChatClient client, string prompt, CancellationToken cancellationToken, int maxAttempts = 3)
    {
        Exception? last = null;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                ChatResponse<T> response =
                    await client.GetResponseAsync<T>(prompt, cancellationToken: cancellationToken);
                return response.Result;
            }
            catch (Exception ex) when (
                (ex is HttpRequestException || ex is TaskCanceledException)
                && !cancellationToken.IsCancellationRequested
                && attempt < maxAttempts)
            {
                last = ex;
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), cancellationToken);
            }
        }
        throw new InvalidOperationException(
            $"LLM call failed after {maxAttempts} attempts.", last);
    }
}

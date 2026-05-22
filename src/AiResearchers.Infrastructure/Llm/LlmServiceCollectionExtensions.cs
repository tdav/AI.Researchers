using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;

namespace AiResearchers.Infrastructure.Llm;

public static class LlmServiceCollectionExtensions
{
    public static IServiceCollection AddLlm(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        LlmOptions options = configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>()
            ?? new LlmOptions();
        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));

        // OllamaApiClient реализует Microsoft.Extensions.AI.IChatClient.
        // Function-invocation pipeline нужен, чтобы tool-calls исполнялись автоматически.
        services.AddSingleton<IChatClient>(_ =>
        {
            OllamaApiClient ollama = new(new Uri(options.Endpoint), options.Model);
            return new ChatClientBuilder(ollama)
                .UseFunctionInvocation()
                .Build();
        });

        return services;
    }
}

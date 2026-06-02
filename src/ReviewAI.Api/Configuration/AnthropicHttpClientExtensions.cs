using Microsoft.Extensions.Http.Resilience;
using Polly;
using ReviewAI.Core.Configuration;

namespace ReviewAI.Api.Configuration;

public static class AnthropicHttpClientExtensions
{
    public const string AnthropicClientName = "Anthropic";

    public static IServiceCollection AddAnthropicResilientHttpClient(
        this IServiceCollection services,
        ResilienceOptions options)
    {
        services.AddHttpClient(AnthropicClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            })
            .AddResilienceHandler("anthropic-pipeline", builder =>
            {
                builder
                    .AddTimeout(TimeSpan.FromSeconds(options.TotalTimeoutSeconds))
                    .AddRetry(new HttpRetryStrategyOptions
                    {
                        MaxRetryAttempts = options.MaxRetries,
                        BackoffType = DelayBackoffType.Exponential,
                        UseJitter = true,
                        Delay = TimeSpan.FromSeconds(options.RetryBaseDelaySeconds)
                    })
                    .AddTimeout(TimeSpan.FromSeconds(options.AttemptTimeoutSeconds));
            });

        return services;
    }
}

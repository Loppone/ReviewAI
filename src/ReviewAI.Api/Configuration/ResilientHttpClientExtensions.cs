using Microsoft.Extensions.Http.Resilience;
using Polly;
using ReviewAI.Core.Configuration;

namespace ReviewAI.Api.Configuration;

/// <summary>
/// Registers named <see cref="HttpClient"/> instances backed by a resilience pipeline. Keeping
/// Polly here (composition root) honours the project rule that no resilience concerns leak into
/// the Core services.
/// </summary>
public static class ResilientHttpClientExtensions
{
    public const string AnthropicClientName = "Anthropic";
    public const string GitHubClientName = "GitHub";

    public static IServiceCollection AddAnthropicResilientHttpClient(
        this IServiceCollection services,
        ClientResilienceOptions options) =>
        services.AddResilientHttpClient(AnthropicClientName, options);

    public static IServiceCollection AddGitHubResilientHttpClient(
        this IServiceCollection services,
        ClientResilienceOptions options) =>
        services.AddResilientHttpClient(GitHubClientName, options);

    /// <summary>
    /// Configures a named client with a pooled <see cref="SocketsHttpHandler"/> and a resilience
    /// pipeline composed (outer → inner) as: total timeout → retry (exponential + jitter, on
    /// transient incl. 429, honouring <c>Retry-After</c>) → circuit breaker → per-attempt timeout.
    /// </summary>
    public static IServiceCollection AddResilientHttpClient(
        this IServiceCollection services,
        string clientName,
        ClientResilienceOptions options)
    {
        services.AddHttpClient(clientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            })
            .AddResilienceHandler($"{clientName.ToLowerInvariant()}-pipeline", builder =>
            {
                // Pipeline order (outer → inner): total timeout → retry → circuit breaker →
                // per-attempt timeout, so the breaker counts each individual attempt.
                builder.AddTimeout(TimeSpan.FromSeconds(options.TotalTimeoutSeconds));

                if (options.MaxRetries > 0)
                {
                    builder.AddRetry(new HttpRetryStrategyOptions
                    {
                        // The standard transient handler retries HTTP 5xx/408/429 and network
                        // errors, and honours the Retry-After header by default. GitHub's primary
                        // rate limit surfaces as 403 (not in this transient set) and is therefore
                        // never retried: those resets can be very long, so retrying would only
                        // burn more quota without improving the chance of success. GitHub's
                        // secondary rate limit (429) is retried with Retry-After backoff.
                        MaxRetryAttempts = options.MaxRetries,
                        BackoffType = DelayBackoffType.Exponential,
                        UseJitter = true,
                        Delay = TimeSpan.FromSeconds(options.RetryBaseDelaySeconds)
                    });
                }

                builder
                    .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                    {
                        FailureRatio = options.CircuitBreakerFailureRatio,
                        SamplingDuration = TimeSpan.FromSeconds(options.CircuitBreakerSamplingDurationSeconds),
                        MinimumThroughput = options.CircuitBreakerMinimumThroughput,
                        BreakDuration = TimeSpan.FromSeconds(options.CircuitBreakerBreakDurationSeconds)
                    })
                    .AddTimeout(TimeSpan.FromSeconds(options.AttemptTimeoutSeconds));
            });

        return services;
    }
}

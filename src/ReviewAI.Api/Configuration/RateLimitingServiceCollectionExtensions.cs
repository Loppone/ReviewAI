using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ReviewAI.Api.Configuration;

/// <summary>
/// Registers the per-API-key fixed-window rate limiter for the review endpoint. Partitions on the
/// non-reversible key fingerprint claim set by the authentication handler, never the raw key.
/// </summary>
public static class RateLimitingServiceCollectionExtensions
{
    /// <summary>Defensive bucket for requests without an identity claim (auth runs first, so this
    /// should not occur in practice; keeps the partitioner total and avoids null keys).</summary>
    internal const string AnonymousPartitionKey = "__anonymous__";

    public static IServiceCollection AddReviewRateLimiter(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(RateLimitPolicies.ReviewPerApiKey, httpContext =>
            {
                var limits = httpContext.RequestServices
                    .GetRequiredService<IOptions<RateLimitOptions>>().Value;

                return RateLimitPartition.GetFixedWindowLimiter<string>(
                    partitionKey: ResolvePartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.PermitLimit,
                        Window = TimeSpan.FromSeconds(limits.WindowSeconds),
                        QueueLimit = limits.QueueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    });
            });

            options.OnRejected = OnRejectedAsync;
        });

        return services;
    }

    /// <summary>
    /// Resolves the rate-limiting partition key from the authenticated identity. Reads the
    /// <see cref="ClaimTypes.NameIdentifier"/> claim (the key fingerprint set at authentication);
    /// falls back to a fixed anonymous bucket when absent. Pure function — unit-testable without a host.
    /// </summary>
    internal static string ResolvePartitionKey(HttpContext httpContext)
    {
        var keyId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(keyId) ? AnonymousPartitionKey : keyId;
    }

    private static async ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;

        int? retryAfterSeconds = null;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            retryAfterSeconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
            response.Headers.RetryAfter = retryAfterSeconds.Value.ToString(CultureInfo.InvariantCulture);
        }

        // RFC 7807 body, consistent with the global exception handler (application/problem+json).
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
            Detail = "API key rate limit exceeded. Retry after the indicated delay."
        };
        if (retryAfterSeconds is not null)
        {
            problem.Extensions["retryAfterSeconds"] = retryAfterSeconds;
        }

        await response.WriteAsJsonAsync(
            problem, problem.GetType(), options: null, contentType: "application/problem+json", cancellationToken);
    }
}

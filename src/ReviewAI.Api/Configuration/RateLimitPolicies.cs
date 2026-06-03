namespace ReviewAI.Api.Configuration;

/// <summary>
/// Named rate-limiting policy identifiers, applied explicitly to endpoints via
/// <c>[EnableRateLimiting]</c>. Avoids magic strings at the registration and endpoint sites.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>Per-API-key fixed-window policy for the cost-bearing review endpoint.</summary>
    public const string ReviewPerApiKey = "review-per-apikey";
}

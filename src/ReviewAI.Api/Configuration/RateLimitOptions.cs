using System.ComponentModel.DataAnnotations;

namespace ReviewAI.Api.Configuration;

/// <summary>
/// Configuration contract for rate limiting the cost-bearing review endpoint. Rate limiting is
/// an HTTP/ASP.NET boundary concern, so this lives in the Api project (not Core). Bound from the
/// "RateLimiting" appsettings section and validated at startup (fail-fast).
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Requests allowed per fixed window, per authenticated API key.</summary>
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; init; } = 10;

    /// <summary>Width of the fixed window, in seconds.</summary>
    [Range(1, int.MaxValue)]
    public int WindowSeconds { get; init; } = 60;

    /// <summary>Queued requests once the limit is hit. 0 = reject immediately (no waiting).</summary>
    [Range(0, int.MaxValue)]
    public int QueueLimit { get; init; }
}

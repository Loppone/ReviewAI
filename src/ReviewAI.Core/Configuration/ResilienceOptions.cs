using System.ComponentModel.DataAnnotations;

namespace ReviewAI.Core.Configuration;

/// <summary>
/// Strongly-typed configuration for the outbound HTTP resilience pipelines applied to the
/// external clients. Bound from the "Resilience" configuration section and validated at startup
/// by <see cref="ResilienceOptionsValidator"/>. Each external client carries its own profile so
/// latency/cost characteristics can be tuned independently.
/// </summary>
public sealed class ResilienceOptions
{
    public const string SectionName = "Resilience";

    /// <summary>
    /// Resilience profile for the Anthropic (Claude) HTTP client.
    /// </summary>
    [Required]
    public ClientResilienceOptions Anthropic { get; set; } = new();

    /// <summary>
    /// Resilience profile for the GitHub (Octokit) HTTP client.
    /// </summary>
    [Required]
    public ClientResilienceOptions GitHub { get; set; } = new();
}

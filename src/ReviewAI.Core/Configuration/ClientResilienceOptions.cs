using System.ComponentModel.DataAnnotations;

namespace ReviewAI.Core.Configuration;

/// <summary>
/// Strongly-typed resilience configuration for a single outbound HTTP client (e.g. Anthropic
/// or GitHub). Bound from a child section of "Resilience" and validated at startup by
/// <see cref="ResilienceOptionsValidator"/>.
/// </summary>
public sealed class ClientResilienceOptions : IValidatableObject
{
    /// <summary>
    /// Maximum number of retry attempts for transient failures (HTTP 5xx, 408, 429, network errors).
    /// </summary>
    [Range(0, 10, ErrorMessage = "MaxRetries must be between 0 and 10.")]
    public int MaxRetries { get; set; }

    /// <summary>
    /// Base delay for the exponential backoff between retries, in seconds.
    /// </summary>
    [Range(0, 60, ErrorMessage = "RetryBaseDelaySeconds must be between 0 and 60.")]
    public int RetryBaseDelaySeconds { get; set; }

    /// <summary>
    /// Timeout for a single attempt, in seconds.
    /// </summary>
    [Range(1, 600, ErrorMessage = "AttemptTimeoutSeconds must be between 1 and 600.")]
    public int AttemptTimeoutSeconds { get; set; }

    /// <summary>
    /// Timeout for the whole request including all retries, in seconds.
    /// </summary>
    [Range(1, 1200, ErrorMessage = "TotalTimeoutSeconds must be between 1 and 1200.")]
    public int TotalTimeoutSeconds { get; set; }

    /// <summary>
    /// Failure ratio (0.0–1.0) within the sampling window above which the circuit breaks.
    /// </summary>
    [Range(0.0, 1.0, ErrorMessage = "CircuitBreakerFailureRatio must be between 0.0 and 1.0.")]
    public double CircuitBreakerFailureRatio { get; set; }

    /// <summary>
    /// Length of the rolling window over which the failure ratio is sampled, in seconds.
    /// </summary>
    [Range(1, 600, ErrorMessage = "CircuitBreakerSamplingDurationSeconds must be between 1 and 600.")]
    public int CircuitBreakerSamplingDurationSeconds { get; set; }

    /// <summary>
    /// Minimum number of actions within the sampling window before the breaker can open.
    /// Polly requires at least 2.
    /// </summary>
    [Range(2, 1000, ErrorMessage = "CircuitBreakerMinimumThroughput must be between 2 and 1000.")]
    public int CircuitBreakerMinimumThroughput { get; set; }

    /// <summary>
    /// Duration the circuit stays open before transitioning to half-open, in seconds.
    /// </summary>
    [Range(1, 600, ErrorMessage = "CircuitBreakerBreakDurationSeconds must be between 1 and 600.")]
    public int CircuitBreakerBreakDurationSeconds { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TotalTimeoutSeconds < AttemptTimeoutSeconds)
        {
            yield return new ValidationResult(
                "TotalTimeoutSeconds must be greater than or equal to AttemptTimeoutSeconds.",
                [nameof(TotalTimeoutSeconds)]);
        }
    }
}

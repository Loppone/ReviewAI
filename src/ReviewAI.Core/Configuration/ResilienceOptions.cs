using System.ComponentModel.DataAnnotations;

namespace ReviewAI.Core.Configuration;

/// <summary>
/// Strongly-typed configuration for the outbound HTTP resilience pipeline applied to
/// external clients. Bound from the "Resilience" configuration section and validated at startup.
/// </summary>
public sealed class ResilienceOptions : IValidatableObject
{
    public const string SectionName = "Resilience";

    /// <summary>
    /// Maximum number of retry attempts for transient failures (HTTP 5xx, 408, 429, network errors).
    /// </summary>
    [Range(0, 10, ErrorMessage = "Resilience:MaxRetries must be between 0 and 10.")]
    public int MaxRetries { get; set; }

    /// <summary>
    /// Base delay for the exponential backoff between retries, in seconds.
    /// </summary>
    [Range(0, 60, ErrorMessage = "Resilience:RetryBaseDelaySeconds must be between 0 and 60.")]
    public int RetryBaseDelaySeconds { get; set; }

    /// <summary>
    /// Timeout for a single attempt, in seconds.
    /// </summary>
    [Range(1, 600, ErrorMessage = "Resilience:AttemptTimeoutSeconds must be between 1 and 600.")]
    public int AttemptTimeoutSeconds { get; set; }

    /// <summary>
    /// Timeout for the whole request including all retries, in seconds.
    /// </summary>
    [Range(1, 1200, ErrorMessage = "Resilience:TotalTimeoutSeconds must be between 1 and 1200.")]
    public int TotalTimeoutSeconds { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TotalTimeoutSeconds < AttemptTimeoutSeconds)
        {
            yield return new ValidationResult(
                "Resilience:TotalTimeoutSeconds must be greater than or equal to Resilience:AttemptTimeoutSeconds.",
                [nameof(TotalTimeoutSeconds)]);
        }
    }
}

using System.ComponentModel.DataAnnotations;

namespace ReviewAI.Core.Configuration;

/// <summary>
/// Strongly-typed configuration for the Anthropic (Claude) integration.
/// Bound from the "Anthropic" configuration section and validated at startup.
/// </summary>
public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    /// <summary>
    /// Anthropic model identifier (e.g. "claude-sonnet-4-5"). Must be a model ID
    /// supported by the configured Anthropic.SDK / API version.
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Anthropic:Model is required.")]
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of output tokens for the review response.
    /// </summary>
    [Range(1, 200_000, ErrorMessage = "Anthropic:MaxTokens must be between 1 and 200000.")]
    public int MaxTokens { get; set; }

    /// <summary>
    /// Sampling temperature in the [0.0, 1.0] range. Lower values yield more
    /// deterministic, structured output.
    /// </summary>
    [Range(0.0, 1.0, ErrorMessage = "Anthropic:Temperature must be between 0.0 and 1.0.")]
    public decimal Temperature { get; set; }
}

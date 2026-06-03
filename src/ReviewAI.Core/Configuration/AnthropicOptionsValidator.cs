using Microsoft.Extensions.Options;

namespace ReviewAI.Core.Configuration;

/// <summary>
/// Semantic validation for <see cref="AnthropicOptions"/>, executed at startup via
/// <c>ValidateOnStart</c>. Enforces the cross-property rules that data annotations cannot
/// express: the configured <see cref="AnthropicOptions.AllowedModels"/> must be a non-empty,
/// duplicate-free subset of <see cref="KnownAnthropicModels"/>, and
/// <see cref="AnthropicOptions.Model"/> must be one of those allowed models. Failure prevents
/// the host from starting (fail-fast), so a misconfigured or unsupported model can never reach
/// runtime.
/// </summary>
public sealed class AnthropicOptionsValidator : IValidateOptions<AnthropicOptions>
{
    public ValidateOptionsResult Validate(string? name, AnthropicOptions options)
    {
        var failures = new List<string>();

        if (options.AllowedModels.Count == 0)
        {
            failures.Add("Anthropic:AllowedModels must contain at least one model.");
        }
        else
        {
            for (var i = 0; i < options.AllowedModels.Count; i++)
            {
                var entry = options.AllowedModels[i];
                if (string.IsNullOrWhiteSpace(entry))
                {
                    failures.Add($"Anthropic:AllowedModels entry at index {i} is empty.");
                }
                else if (!KnownAnthropicModels.All.Contains(entry))
                {
                    failures.Add($"Anthropic:AllowedModels entry at index {i} ('{entry}') is not a model supported by ReviewAI.");
                }
            }

            if (options.AllowedModels.Distinct(StringComparer.Ordinal).Count() != options.AllowedModels.Count)
            {
                failures.Add("Anthropic:AllowedModels contains duplicate models.");
            }
        }

        if (!options.AllowedModels.Contains(options.Model, StringComparer.Ordinal))
        {
            failures.Add($"Anthropic:Model ('{options.Model}') must be one of the configured AllowedModels.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}

using Microsoft.Extensions.Options;

namespace ReviewAI.Api.Configuration;

/// <summary>
/// Semantic validation for <see cref="ApiKeyAuthOptions"/>, executed at startup via
/// <c>ValidateOnStart</c>. Failure prevents the host from starting (fail-fast), so the
/// authenticated endpoint is never exposed without valid keys.
/// Validation messages reference key positions by index only and never echo key material.
/// </summary>
public sealed class ApiKeyAuthOptionsValidator : IValidateOptions<ApiKeyAuthOptions>
{
    /// <summary>
    /// Minimum allowed length for an API key. Hard-coded application security rule,
    /// not an operational preference.
    /// </summary>
    private const int MinKeyLength = 32;

    public ValidateOptionsResult Validate(string? name, ApiKeyAuthOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.HeaderName))
        {
            failures.Add("ApiKeyAuth:HeaderName is required.");
        }

        if (options.ApiKeys.Count == 0)
        {
            failures.Add("REVIEWAI_API_KEYS must contain at least one API key.");
        }
        else
        {
            for (var i = 0; i < options.ApiKeys.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(options.ApiKeys[i]))
                {
                    failures.Add($"REVIEWAI_API_KEYS entry at index {i} is empty.");
                }
                else if (options.ApiKeys[i].Length < MinKeyLength)
                {
                    failures.Add($"REVIEWAI_API_KEYS entry at index {i} is shorter than the required {MinKeyLength} characters.");
                }
            }

            if (options.ApiKeys.Distinct(StringComparer.Ordinal).Count() != options.ApiKeys.Count)
            {
                failures.Add("REVIEWAI_API_KEYS contains duplicate keys.");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}

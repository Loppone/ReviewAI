using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace ReviewAI.Core.Configuration;

/// <summary>
/// Startup validation for <see cref="ResilienceOptions"/>, executed via <c>ValidateOnStart</c>.
/// This validator is the single point responsible for resilience configuration validation: it
/// explicitly validates both the <see cref="ResilienceOptions.Anthropic"/> and
/// <see cref="ResilienceOptions.GitHub"/> sub-objects with
/// <see cref="Validator.TryValidateObject(object, ValidationContext, ICollection{ValidationResult}, bool)"/>
/// (running both their data annotations and the <see cref="IValidatableObject"/> cross-field
/// rule). The default <c>ValidateDataAnnotations()</c> does not recurse into nested objects, so
/// the recursion is performed here rather than relying on custom attributes. Failure prevents the
/// host from starting (fail-fast).
/// </summary>
public sealed class ResilienceOptionsValidator : IValidateOptions<ResilienceOptions>
{
    public ValidateOptionsResult Validate(string? name, ResilienceOptions options)
    {
        var failures = new List<string>();

        ValidateClient(nameof(ResilienceOptions.Anthropic), options.Anthropic, failures);
        ValidateClient(nameof(ResilienceOptions.GitHub), options.GitHub, failures);

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static void ValidateClient(string section, ClientResilienceOptions client, List<string> failures)
    {
        if (client is null)
        {
            failures.Add($"Resilience:{section} must be provided.");
            return;
        }

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(client, new ValidationContext(client), results, validateAllProperties: true);

        foreach (var result in results)
        {
            failures.Add($"Resilience:{section}: {result.ErrorMessage}");
        }
    }
}

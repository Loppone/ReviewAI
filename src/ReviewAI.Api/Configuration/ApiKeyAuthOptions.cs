using System.ComponentModel.DataAnnotations;

namespace ReviewAI.Api.Configuration;

/// <summary>
/// Configuration contract for API key authentication.
/// Non-secret knobs (header name) are bound from the "ApiKeyAuth" appsettings section;
/// the API keys themselves are populated exclusively from the REVIEWAI_API_KEYS
/// environment variable and never read from configuration files.
/// </summary>
public sealed class ApiKeyAuthOptions
{
    public const string SectionName = "ApiKeyAuth";

    /// <summary>HTTP header carrying the API key. Non-secret, from appsettings.</summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "ApiKeyAuth:HeaderName is required.")]
    public string HeaderName { get; set; } = "X-API-Key";

    /// <summary>
    /// Valid API keys. Populated from the REVIEWAI_API_KEYS environment variable only —
    /// never bound from configuration files. All listed keys are accepted simultaneously
    /// to support zero-downtime key rotation.
    /// </summary>
    public IReadOnlyList<string> ApiKeys { get; set; } = [];
}

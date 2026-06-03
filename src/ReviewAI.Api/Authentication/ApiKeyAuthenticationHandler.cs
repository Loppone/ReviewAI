using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ReviewAI.Api.Configuration;

namespace ReviewAI.Api.Authentication;

/// <summary>
/// Authenticates requests by matching the configured header against the set of valid
/// API keys. Comparison is constant-time and does not short-circuit across keys, so the
/// response time reveals neither which key matched nor how many keys are configured.
/// No key material is ever written to logs or failure messages.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<ApiKeyAuthOptions> apiKeyOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private readonly ApiKeyAuthOptions _apiKeyOptions = apiKeyOptions.Value;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(_apiKeyOptions.HeaderName, out var headerValues))
        {
            // No credentials presented — let the pipeline issue a challenge.
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var providedKey = headerValues.ToString();
        if (string.IsNullOrWhiteSpace(providedKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing API key."));
        }

        if (!IsKnownKey(providedKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var identity = new ClaimsIdentity(ApiKeyAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationDefaults.AuthenticationScheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private bool IsKnownKey(string providedKey)
    {
        var providedBytes = Encoding.UTF8.GetBytes(providedKey);
        var match = false;

        // Iterate every configured key without short-circuiting. FixedTimeEquals is
        // constant-time per comparison (and returns false on length mismatch); the
        // bitwise accumulation keeps the total work independent of where a match occurs.
        foreach (var candidate in _apiKeyOptions.ApiKeys)
        {
            var candidateBytes = Encoding.UTF8.GetBytes(candidate);
            match |= CryptographicOperations.FixedTimeEquals(providedBytes, candidateBytes);
        }

        return match;
    }
}

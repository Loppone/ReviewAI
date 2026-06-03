namespace ReviewAI.Api.Authentication;

/// <summary>
/// Default values for API key authentication. Mirrors the framework convention
/// (e.g. <c>JwtBearerDefaults</c>, <c>CookieAuthenticationDefaults</c>).
/// </summary>
public static class ApiKeyAuthenticationDefaults
{
    public const string AuthenticationScheme = "ApiKey";
}

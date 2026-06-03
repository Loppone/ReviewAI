using FluentAssertions;
using Microsoft.Extensions.Options;
using ReviewAI.Api.Configuration;

namespace ReviewAI.Tests;

public class ApiKeyAuthOptionsValidatorTests
{
    private const string ValidKey = "0123456789abcdef0123456789abcdef"; // 32 chars
    private const string AnotherValidKey = "fedcba9876543210fedcba9876543210"; // 32 chars

    private static ValidateOptionsResult Validate(ApiKeyAuthOptions options) =>
        new ApiKeyAuthOptionsValidator().Validate(name: null, options);

    [Fact]
    public void Validate_WithSingleValidKey_Succeeds()
    {
        var options = new ApiKeyAuthOptions { HeaderName = "X-API-Key", ApiKeys = [ValidKey] };

        Validate(options).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithMultipleValidKeys_Succeeds()
    {
        var options = new ApiKeyAuthOptions { HeaderName = "X-API-Key", ApiKeys = [ValidKey, AnotherValidKey] };

        Validate(options).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNoKeys_Fails()
    {
        var options = new ApiKeyAuthOptions { HeaderName = "X-API-Key", ApiKeys = [] };

        var result = Validate(options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("at least one API key"));
    }

    [Fact]
    public void Validate_WithShortKey_Fails()
    {
        var options = new ApiKeyAuthOptions { HeaderName = "X-API-Key", ApiKeys = ["tooshort"] };

        var result = Validate(options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("index 0") && f.Contains("32 characters"));
    }

    [Fact]
    public void Validate_WithWhitespaceKey_Fails()
    {
        var options = new ApiKeyAuthOptions { HeaderName = "X-API-Key", ApiKeys = ["   "] };

        var result = Validate(options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("index 0") && f.Contains("empty"));
    }

    [Fact]
    public void Validate_WithDuplicateKeys_Fails()
    {
        var options = new ApiKeyAuthOptions { HeaderName = "X-API-Key", ApiKeys = [ValidKey, ValidKey] };

        var result = Validate(options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("duplicate"));
    }

    [Fact]
    public void Validate_WithEmptyHeaderName_Fails()
    {
        var options = new ApiKeyAuthOptions { HeaderName = "  ", ApiKeys = [ValidKey] };

        var result = Validate(options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("HeaderName"));
    }

    [Fact]
    public void Validate_FailureMessages_DoNotLeakKeyMaterial()
    {
        var options = new ApiKeyAuthOptions { HeaderName = "X-API-Key", ApiKeys = ["short-secret-value"] };

        var result = Validate(options);

        result.Failures.Should().NotContain(f => f.Contains("short-secret-value"));
    }
}

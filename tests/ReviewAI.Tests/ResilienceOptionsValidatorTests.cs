using FluentAssertions;
using Microsoft.Extensions.Options;
using ReviewAI.Core.Configuration;

namespace ReviewAI.Tests;

public class ResilienceOptionsValidatorTests
{
    private static ClientResilienceOptions ValidClient() => new()
    {
        MaxRetries = 3,
        RetryBaseDelaySeconds = 2,
        AttemptTimeoutSeconds = 30,
        TotalTimeoutSeconds = 120,
        CircuitBreakerFailureRatio = 0.5,
        CircuitBreakerSamplingDurationSeconds = 30,
        CircuitBreakerMinimumThroughput = 10,
        CircuitBreakerBreakDurationSeconds = 15
    };

    private static ValidateOptionsResult Validate(ResilienceOptions options) =>
        new ResilienceOptionsValidator().Validate(name: null, options);

    [Fact]
    public void Validate_WithValidProfiles_Succeeds()
    {
        var options = new ResilienceOptions { Anthropic = ValidClient(), GitHub = ValidClient() };

        Validate(options).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenAnthropicProfileInvalid_Fails()
    {
        var anthropic = ValidClient();
        anthropic.MaxRetries = 99;
        var options = new ResilienceOptions { Anthropic = anthropic, GitHub = ValidClient() };

        var result = Validate(options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("Anthropic") && f.Contains("MaxRetries"));
    }

    [Fact]
    public void Validate_WhenGitHubProfileInvalid_Fails()
    {
        var github = ValidClient();
        github.CircuitBreakerMinimumThroughput = 1;
        var options = new ResilienceOptions { Anthropic = ValidClient(), GitHub = github };

        var result = Validate(options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("GitHub") && f.Contains("CircuitBreakerMinimumThroughput"));
    }

    [Fact]
    public void Validate_WhenTotalTimeoutBelowAttemptTimeout_FailsViaCrossFieldRule()
    {
        var github = ValidClient();
        github.AttemptTimeoutSeconds = 60;
        github.TotalTimeoutSeconds = 30;
        var options = new ResilienceOptions { Anthropic = ValidClient(), GitHub = github };

        var result = Validate(options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("GitHub") && f.Contains("TotalTimeoutSeconds"));
    }
}

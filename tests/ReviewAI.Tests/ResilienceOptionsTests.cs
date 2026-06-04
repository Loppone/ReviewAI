using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using ReviewAI.Core.Configuration;

namespace ReviewAI.Tests;

public class ResilienceOptionsTests
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

    private static IReadOnlyList<ValidationResult> Validate(ClientResilienceOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Validate_WithValidClientOptions_ProducesNoErrors()
    {
        Validate(ValidClient()).Should().BeEmpty();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(61)]
    public void Validate_WithRetryBaseDelayOutOfRange_FailsValidation(int retryBaseDelay)
    {
        var options = ValidClient();
        options.RetryBaseDelaySeconds = retryBaseDelay;

        Validate(options).Should().Contain(r => r.MemberNames.Contains(nameof(ClientResilienceOptions.RetryBaseDelaySeconds)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void Validate_WithMaxRetriesOutOfRange_FailsValidation(int maxRetries)
    {
        var options = ValidClient();
        options.MaxRetries = maxRetries;

        Validate(options).Should().Contain(r => r.MemberNames.Contains(nameof(ClientResilienceOptions.MaxRetries)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_WithNonPositiveAttemptTimeout_FailsValidation(int attemptTimeout)
    {
        var options = ValidClient();
        options.AttemptTimeoutSeconds = attemptTimeout;

        Validate(options).Should().Contain(r => r.MemberNames.Contains(nameof(ClientResilienceOptions.AttemptTimeoutSeconds)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_WithNonPositiveTotalTimeout_FailsValidation(int totalTimeout)
    {
        var options = ValidClient();
        options.TotalTimeoutSeconds = totalTimeout;

        Validate(options).Should().Contain(r => r.MemberNames.Contains(nameof(ClientResilienceOptions.TotalTimeoutSeconds)));
    }

    [Fact]
    public void Validate_WhenTotalTimeoutBelowAttemptTimeout_FailsValidation()
    {
        var options = ValidClient();
        options.AttemptTimeoutSeconds = 60;
        options.TotalTimeoutSeconds = 30;

        Validate(options).Should().Contain(r => r.MemberNames.Contains(nameof(ClientResilienceOptions.TotalTimeoutSeconds)));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Validate_WithCircuitBreakerFailureRatioOutOfRange_FailsValidation(double ratio)
    {
        var options = ValidClient();
        options.CircuitBreakerFailureRatio = ratio;

        Validate(options).Should().Contain(r => r.MemberNames.Contains(nameof(ClientResilienceOptions.CircuitBreakerFailureRatio)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(601)]
    public void Validate_WithCircuitBreakerSamplingDurationOutOfRange_FailsValidation(int sampling)
    {
        var options = ValidClient();
        options.CircuitBreakerSamplingDurationSeconds = sampling;

        Validate(options).Should().Contain(r => r.MemberNames.Contains(nameof(ClientResilienceOptions.CircuitBreakerSamplingDurationSeconds)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1001)]
    public void Validate_WithCircuitBreakerMinimumThroughputOutOfRange_FailsValidation(int throughput)
    {
        var options = ValidClient();
        options.CircuitBreakerMinimumThroughput = throughput;

        Validate(options).Should().Contain(r => r.MemberNames.Contains(nameof(ClientResilienceOptions.CircuitBreakerMinimumThroughput)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(601)]
    public void Validate_WithCircuitBreakerBreakDurationOutOfRange_FailsValidation(int breakDuration)
    {
        var options = ValidClient();
        options.CircuitBreakerBreakDurationSeconds = breakDuration;

        Validate(options).Should().Contain(r => r.MemberNames.Contains(nameof(ClientResilienceOptions.CircuitBreakerBreakDurationSeconds)));
    }

    [Fact]
    public void ResilienceOptions_DefaultsToNonNullClientProfiles()
    {
        var options = new ResilienceOptions();

        options.Anthropic.Should().NotBeNull();
        options.GitHub.Should().NotBeNull();
    }
}

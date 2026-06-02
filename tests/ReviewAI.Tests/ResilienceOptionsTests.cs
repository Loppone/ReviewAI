using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using ReviewAI.Core.Configuration;

namespace ReviewAI.Tests;

public class ResilienceOptionsTests
{
    private static IReadOnlyList<ValidationResult> Validate(ResilienceOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Validate_WithValidOptions_ProducesNoErrors()
    {
        var options = new ResilienceOptions { MaxRetries = 3, RetryBaseDelaySeconds = 2, AttemptTimeoutSeconds = 30, TotalTimeoutSeconds = 120 };

        Validate(options).Should().BeEmpty();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(61)]
    public void Validate_WithRetryBaseDelayOutOfRange_FailsValidation(int retryBaseDelay)
    {
        var options = new ResilienceOptions { MaxRetries = 3, RetryBaseDelaySeconds = retryBaseDelay, AttemptTimeoutSeconds = 30, TotalTimeoutSeconds = 120 };

        Validate(options).Should().Contain(r => r.MemberNames.Contains(nameof(ResilienceOptions.RetryBaseDelaySeconds)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void Validate_WithMaxRetriesOutOfRange_FailsValidation(int maxRetries)
    {
        var options = new ResilienceOptions { MaxRetries = maxRetries, AttemptTimeoutSeconds = 30, TotalTimeoutSeconds = 120 };

        Validate(options).Should().Contain(r => r.MemberNames.Contains(nameof(ResilienceOptions.MaxRetries)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_WithNonPositiveAttemptTimeout_FailsValidation(int attemptTimeout)
    {
        var options = new ResilienceOptions { MaxRetries = 3, AttemptTimeoutSeconds = attemptTimeout, TotalTimeoutSeconds = 120 };

        Validate(options).Should().Contain(r => r.MemberNames.Contains(nameof(ResilienceOptions.AttemptTimeoutSeconds)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_WithNonPositiveTotalTimeout_FailsValidation(int totalTimeout)
    {
        var options = new ResilienceOptions { MaxRetries = 3, AttemptTimeoutSeconds = 30, TotalTimeoutSeconds = totalTimeout };

        Validate(options).Should().Contain(r => r.MemberNames.Contains(nameof(ResilienceOptions.TotalTimeoutSeconds)));
    }

    [Fact]
    public void Validate_WhenTotalTimeoutBelowAttemptTimeout_FailsValidation()
    {
        var options = new ResilienceOptions { MaxRetries = 3, AttemptTimeoutSeconds = 60, TotalTimeoutSeconds = 30 };

        Validate(options).Should().Contain(r => r.MemberNames.Contains(nameof(ResilienceOptions.TotalTimeoutSeconds)));
    }
}

using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using ReviewAI.Api.Configuration;

namespace ReviewAI.Tests;

public class RateLimitOptionsTests
{
    private static IReadOnlyList<ValidationResult> Validate(RateLimitOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Validate_WithDefaults_Succeeds()
    {
        Validate(new RateLimitOptions()).Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithValidValues_Succeeds()
    {
        var options = new RateLimitOptions { PermitLimit = 5, WindowSeconds = 30, QueueLimit = 0 };

        Validate(options).Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithNonPositivePermitLimit_Fails()
    {
        var options = new RateLimitOptions { PermitLimit = 0 };

        Validate(options).Should().Contain(r => r.MemberNames.Contains(nameof(RateLimitOptions.PermitLimit)));
    }

    [Fact]
    public void Validate_WithNonPositiveWindowSeconds_Fails()
    {
        var options = new RateLimitOptions { WindowSeconds = 0 };

        Validate(options).Should().Contain(r => r.MemberNames.Contains(nameof(RateLimitOptions.WindowSeconds)));
    }

    [Fact]
    public void Validate_WithNegativeQueueLimit_Fails()
    {
        var options = new RateLimitOptions { QueueLimit = -1 };

        Validate(options).Should().Contain(r => r.MemberNames.Contains(nameof(RateLimitOptions.QueueLimit)));
    }
}

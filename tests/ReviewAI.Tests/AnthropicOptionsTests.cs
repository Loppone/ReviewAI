using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using ReviewAI.Core.Configuration;

namespace ReviewAI.Tests;

public class AnthropicOptionsTests
{
    private static IReadOnlyList<ValidationResult> Validate(AnthropicOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Validate_WithValidOptions_ProducesNoErrors()
    {
        var options = new AnthropicOptions { Model = "claude-sonnet-4-5", MaxTokens = 2048, Temperature = 0.2m };

        Validate(options).Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithMissingModel_FailsValidation(string model)
    {
        var options = new AnthropicOptions { Model = model, MaxTokens = 2048, Temperature = 0.2m };

        Validate(options).Should().Contain(r => r.MemberNames.Contains(nameof(AnthropicOptions.Model)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithNonPositiveMaxTokens_FailsValidation(int maxTokens)
    {
        var options = new AnthropicOptions { Model = "claude-sonnet-4-5", MaxTokens = maxTokens, Temperature = 0.2m };

        Validate(options).Should().Contain(r => r.MemberNames.Contains(nameof(AnthropicOptions.MaxTokens)));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Validate_WithTemperatureOutOfRange_FailsValidation(double temperature)
    {
        var options = new AnthropicOptions { Model = "claude-sonnet-4-5", MaxTokens = 2048, Temperature = (decimal)temperature };

        Validate(options).Should().Contain(r => r.MemberNames.Contains(nameof(AnthropicOptions.Temperature)));
    }
}

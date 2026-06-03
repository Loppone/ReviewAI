using FluentAssertions;
using Microsoft.Extensions.Options;
using ReviewAI.Core.Configuration;

namespace ReviewAI.Tests;

public class AnthropicOptionsValidatorTests
{
    private const string KnownModel = KnownAnthropicModels.ClaudeSonnet45;
    private const string AnotherKnownModel = KnownAnthropicModels.ClaudeSonnet46;

    private static AnthropicOptions Options(string model, params string[] allowedModels) =>
        new() { Model = model, MaxTokens = 2048, Temperature = 0.2m, AllowedModels = allowedModels };

    private static ValidateOptionsResult Validate(AnthropicOptions options) =>
        new AnthropicOptionsValidator().Validate(name: null, options);

    [Fact]
    public void Validate_WithModelInKnownAllowedModels_Succeeds()
    {
        var result = Validate(Options(KnownModel, KnownModel, AnotherKnownModel));

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyAllowedModels_Fails()
    {
        var result = Validate(Options(KnownModel));

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("at least one model"));
    }

    [Fact]
    public void Validate_WithUnsupportedAllowedModel_Fails()
    {
        var result = Validate(Options(KnownModel, KnownModel, "claude-made-up-9"));

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("index 1") && f.Contains("not a model supported"));
    }

    [Fact]
    public void Validate_WithModelNotInAllowedModels_Fails()
    {
        var result = Validate(Options(AnotherKnownModel, KnownModel));

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("must be one of the configured AllowedModels"));
    }

    [Fact]
    public void Validate_WithDuplicateAllowedModels_Fails()
    {
        var result = Validate(Options(KnownModel, KnownModel, KnownModel));

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("duplicate"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyAllowedModelEntry_Fails(string entry)
    {
        var result = Validate(Options(KnownModel, KnownModel, entry));

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("index 1") && f.Contains("empty"));
    }
}

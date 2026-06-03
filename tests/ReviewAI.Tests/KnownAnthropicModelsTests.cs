using FluentAssertions;
using ReviewAI.Core.Configuration;

namespace ReviewAI.Tests;

public class KnownAnthropicModelsTests
{
    [Fact]
    public void All_IsNotEmpty()
    {
        KnownAnthropicModels.All.Should().NotBeEmpty();
    }

    [Fact]
    public void All_ContainsDeclaredModelConstants()
    {
        KnownAnthropicModels.All.Should().Contain(new[]
        {
            KnownAnthropicModels.ClaudeSonnet45,
            KnownAnthropicModels.ClaudeSonnet46,
            KnownAnthropicModels.ClaudeOpus46,
            KnownAnthropicModels.ClaudeHaiku45
        });
    }
}

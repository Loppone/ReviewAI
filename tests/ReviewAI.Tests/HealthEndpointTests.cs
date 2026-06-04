using System.Net;
using FluentAssertions;
using NSubstitute;
using ReviewAI.Core.Services;
using ReviewAI.Tests.Infrastructure;

namespace ReviewAI.Tests;

public class HealthEndpointTests
{
    [Fact]
    public async Task Health_WithoutApiKey_ReturnsOkWithoutCallingExternalServices()
    {
        var gitHubDiffService = Substitute.For<IGitHubDiffService>();
        var claudeReviewService = Substitute.For<IClaudeReviewService>();

        await using var factory = new ReviewAiWebApplicationFactory(gitHubDiffService, claudeReviewService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await gitHubDiffService.DidNotReceive()
            .GetPullRequestDiff(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await claudeReviewService.DidNotReceive()
            .ReviewDiffAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}

using FluentAssertions;
using NSubstitute;
using ReviewAI.Core.Features.ReviewPullRequest;
using ReviewAI.Core.Services;

namespace ReviewAI.Tests;

public class ReviewPullRequestHandlerTests
{
    [Fact]
    public async Task Handle_WithValidPullRequestUrl_ReturnsStructuredReview()
    {
        var gitHubDiffService = Substitute.For<IGitHubDiffService>();
        var claudeReviewService = Substitute.For<IClaudeReviewService>();
        var handler = new ReviewPullRequestHandler(gitHubDiffService, claudeReviewService);

        var diff = "diff --git a/Program.cs b/Program.cs";
        gitHubDiffService.GetPullRequestDiff(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(diff);

        var expected = new ReviewPullRequestResult(
            Summary: "Review summary",
            SeverityScore: 4.2m,
            SecurityIssues: new[] { "Use parameter validation." },
            PerformanceIssues: new[] { "Avoid repeated allocations." },
            NamingIssues: new[] { "Rename variable for clarity." },
            PatternIssues: new[] { "Prefer single responsibility." });

        claudeReviewService.ReviewDiffAsync(diff, Arg.Any<CancellationToken>())
            .Returns(expected);

        var command = new ReviewPullRequestCommand("https://github.com/owner/repo/pull/42");
        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        await gitHubDiffService.Received(1).GetPullRequestDiff(command.PullRequestUrl, Arg.Any<CancellationToken>());
        await claudeReviewService.Received(1).ReviewDiffAsync(diff, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenGitHubDiffServiceThrows_PropagatesException()
    {
        var gitHubDiffService = Substitute.For<IGitHubDiffService>();
        var claudeReviewService = Substitute.For<IClaudeReviewService>();
        var handler = new ReviewPullRequestHandler(gitHubDiffService, claudeReviewService);

        gitHubDiffService.GetPullRequestDiff(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new InvalidOperationException("GitHub failure")));

        var command = new ReviewPullRequestCommand("https://github.com/owner/repo/pull/42");

        await FluentActions.Invoking(() => handler.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("GitHub failure");
    }
}

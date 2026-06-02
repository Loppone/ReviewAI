using FluentAssertions;
using FluentResults;
using NSubstitute;
using ReviewAI.Core.Common.Errors;
using ReviewAI.Core.Features.ReviewPullRequest;
using ReviewAI.Core.Services;

namespace ReviewAI.Tests;

public class ReviewPullRequestHandlerTests
{
    private readonly IGitHubDiffService _gitHubDiffService = Substitute.For<IGitHubDiffService>();
    private readonly IClaudeReviewService _claudeReviewService = Substitute.For<IClaudeReviewService>();

    private ReviewPullRequestHandler CreateHandler() => new(_gitHubDiffService, _claudeReviewService);

    [Fact]
    public async Task Handle_WithValidPullRequestUrl_ReturnsSuccessfulResult()
    {
        var handler = CreateHandler();

        var diff = "diff --git a/Program.cs b/Program.cs";
        _gitHubDiffService.GetPullRequestDiff(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(diff));

        var expected = new ReviewPullRequestResult(
            Summary: "Review summary",
            SeverityScore: 4.2m,
            SecurityIssues: new[] { "Use parameter validation." },
            PerformanceIssues: new[] { "Avoid repeated allocations." },
            NamingIssues: new[] { "Rename variable for clarity." },
            PatternIssues: new[] { "Prefer single responsibility." });

        _claudeReviewService.ReviewDiffAsync(diff, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(expected));

        var command = new ReviewPullRequestCommand("https://github.com/owner/repo/pull/42");
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expected);
        await _gitHubDiffService.Received(1).GetPullRequestDiff(command.PullRequestUrl, Arg.Any<CancellationToken>());
        await _claudeReviewService.Received(1).ReviewDiffAsync(diff, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenGitHubDiffServiceFails_ReturnsFailedResult()
    {
        var handler = CreateHandler();

        _gitHubDiffService.GetPullRequestDiff(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<string>(new ValidationError("Invalid GitHub pull request URL.")));

        var command = new ReviewPullRequestCommand("https://github.com/owner/repo/pull/42");
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.HasError<ValidationError>().Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenGitHubDiffServiceFails_DoesNotCallClaude()
    {
        var handler = CreateHandler();

        _gitHubDiffService.GetPullRequestDiff(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<string>(new ExternalServiceError("GitHub failure")));

        var command = new ReviewPullRequestCommand("https://github.com/owner/repo/pull/42");
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        await _claudeReviewService.DidNotReceive()
            .ReviewDiffAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenClaudeReviewFails_ReturnsThatFailure()
    {
        var handler = CreateHandler();

        var diff = "diff --git a/Program.cs b/Program.cs";
        _gitHubDiffService.GetPullRequestDiff(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(diff));
        _claudeReviewService.ReviewDiffAsync(diff, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<ReviewPullRequestResult>(new InvalidAiResponseError("Bad JSON")));

        var command = new ReviewPullRequestCommand("https://github.com/owner/repo/pull/42");
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.HasError<InvalidAiResponseError>().Should().BeTrue();
    }
}

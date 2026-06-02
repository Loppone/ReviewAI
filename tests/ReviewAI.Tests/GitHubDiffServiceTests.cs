using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Octokit;
using ReviewAI.Core.Common.Errors;
using ReviewAI.Core.Services;

namespace ReviewAI.Tests;

public class GitHubDiffServiceTests
{
    private readonly IGitHubClient _gitHubClient = Substitute.For<IGitHubClient>();

    private GitHubDiffService CreateService() => new(_gitHubClient, NullLogger<GitHubDiffService>.Instance);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("https://github.com/owner/repo")]
    [InlineData("https://github.com/owner/repo/issues/42")]
    [InlineData("https://github.com/owner/repo/pull/not-a-number")]
    public async Task GetPullRequestDiff_WithInvalidUrl_ReturnsValidationError(string url)
    {
        var service = CreateService();

        var result = await service.GetPullRequestDiff(url, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.HasError<ValidationError>().Should().BeTrue();
    }

    [Fact]
    public async Task GetPullRequestDiff_WhenPullRequestNotFound_ReturnsNotFoundError()
    {
        var service = CreateService();
        _gitHubClient.Repository.PullRequest.Get("owner", "repo", 42)
            .Returns<Task<PullRequest>>(_ => throw new NotFoundException("not found", HttpStatusCode.NotFound));

        var result = await service.GetPullRequestDiff("https://github.com/owner/repo/pull/42", CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.HasError<NotFoundError>().Should().BeTrue();
    }

    [Fact]
    public async Task GetPullRequestDiff_WhenGitHubApiFails_ReturnsExternalServiceError()
    {
        var service = CreateService();
        _gitHubClient.Repository.PullRequest.Get("owner", "repo", 42)
            .Returns<Task<PullRequest>>(_ => throw new ApiException("boom", HttpStatusCode.InternalServerError));

        var result = await service.GetPullRequestDiff("https://github.com/owner/repo/pull/42", CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.HasError<ExternalServiceError>().Should().BeTrue();
    }

    [Fact]
    public async Task GetPullRequestDiff_ForwardsCancellationTokenToDiffDownload()
    {
        var service = CreateService();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _gitHubClient.Repository.PullRequest.Get("owner", "repo", 42)
            .Returns(PullRequestWithDiffUrl("https://github.com/owner/repo/pull/42.diff"));

        var apiResponse = Substitute.For<IApiResponse<string>>();
        apiResponse.Body.Returns("diff-content");
        _gitHubClient.Connection.Get<string>(Arg.Any<Uri>(), Arg.Any<IDictionary<string, string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(apiResponse);

        var result = await service.GetPullRequestDiff("https://github.com/owner/repo/pull/42", token);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("diff-content");
        await _gitHubClient.Connection.Received(1).Get<string>(
            Arg.Any<Uri>(), Arg.Any<IDictionary<string, string>>(), "application/vnd.github.v3.diff", token);
    }

    private static PullRequest PullRequestWithDiffUrl(string diffUrl)
    {
        var pullRequest = new PullRequest();
        typeof(PullRequest).GetProperty(nameof(PullRequest.DiffUrl))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(pullRequest, [diffUrl]);
        return pullRequest;
    }
}

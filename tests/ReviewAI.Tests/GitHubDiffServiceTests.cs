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
}

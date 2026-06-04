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
    private const string DiffMediaType = "application/vnd.github.v3.diff";
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
        DiffCall().Returns<Task<IApiResponse<string>>>(_ => throw new NotFoundException("not found", HttpStatusCode.NotFound));

        var result = await service.GetPullRequestDiff("https://github.com/owner/repo/pull/42", CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.HasError<NotFoundError>().Should().BeTrue();
    }

    [Fact]
    public async Task GetPullRequestDiff_WhenGitHubApiFails_ReturnsExternalServiceError()
    {
        var service = CreateService();
        DiffCall().Returns<Task<IApiResponse<string>>>(_ => throw new ApiException("boom", HttpStatusCode.InternalServerError));

        var result = await service.GetPullRequestDiff("https://github.com/owner/repo/pull/42", CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.HasError<ExternalServiceError>().Should().BeTrue();
    }

    [Fact]
    public async Task GetPullRequestDiff_WhenRateLimited_ReturnsExternalServiceError()
    {
        var service = CreateService();
        var rateLimit = new RateLimit(5000, 0, DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds());
        var apiInfo = new ApiInfo(
            new Dictionary<string, Uri>(),
            new List<string>(),
            new List<string>(),
            "etag",
            rateLimit,
            TimeSpan.Zero);
        var response = Substitute.For<IResponse>();
        response.ApiInfo.Returns(apiInfo);
        DiffCall().Returns<Task<IApiResponse<string>>>(_ => throw new RateLimitExceededException(response));

        var result = await service.GetPullRequestDiff("https://github.com/owner/repo/pull/42", CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.HasError<ExternalServiceError>().Should().BeTrue();
    }

    [Fact]
    public async Task GetPullRequestDiff_FetchesDiffInSingleCancellableCall()
    {
        var service = CreateService();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var apiResponse = Substitute.For<IApiResponse<string>>();
        apiResponse.Body.Returns("diff-content");
        DiffCall(token).Returns(apiResponse);

        var result = await service.GetPullRequestDiff("https://github.com/owner/repo/pull/42", token);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("diff-content");

        // A single cancellable call against the pull request endpoint with the diff media type.
        await _gitHubClient.Connection.Received(1).Get<string>(
            Arg.Is<Uri>(u => u.ToString() == "repos/owner/repo/pulls/42"),
            Arg.Any<IDictionary<string, string>>(),
            DiffMediaType,
            token);
        await _gitHubClient.Repository.PullRequest.DidNotReceiveWithAnyArgs().Get(default!, default!, default);
    }

    [Fact]
    public async Task GetPullRequestDiff_WithEmptyDiffBody_ReturnsEmptyString()
    {
        var service = CreateService();
        var apiResponse = Substitute.For<IApiResponse<string>>();
        apiResponse.Body.Returns((string?)null);
        DiffCall().Returns(apiResponse);

        var result = await service.GetPullRequestDiff("https://github.com/owner/repo/pull/42", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    private Task<IApiResponse<string>> DiffCall(CancellationToken token) =>
        _gitHubClient.Connection.Get<string>(Arg.Any<Uri>(), Arg.Any<IDictionary<string, string>>(), DiffMediaType, token);

    private Task<IApiResponse<string>> DiffCall() =>
        _gitHubClient.Connection.Get<string>(Arg.Any<Uri>(), Arg.Any<IDictionary<string, string>>(), DiffMediaType, Arg.Any<CancellationToken>());
}

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FluentResults;
using Microsoft.AspNetCore.Mvc.Testing;
using NSubstitute;
using ReviewAI.Api.Controllers;
using ReviewAI.Core.Common.Errors;
using ReviewAI.Core.Features.ReviewPullRequest;
using ReviewAI.Core.Services;
using ReviewAI.Tests.Infrastructure;

namespace ReviewAI.Tests;

public class ReviewPullRequestEndpointTests
{
    [Fact]
    public async Task ReviewPullRequest_WithSuccessfulReview_ReturnsOkWithSerializedPayload()
    {
        var gitHubDiffService = Substitute.For<IGitHubDiffService>();
        var claudeReviewService = Substitute.For<IClaudeReviewService>();
        var diff = "diff --git a/Program.cs b/Program.cs";
        var expected = new ReviewPullRequestResult(
            Summary: "Review summary",
            SeverityScore: 4.2m,
            SecurityIssues: ["Use parameter validation."],
            PerformanceIssues: ["Avoid repeated allocations."],
            NamingIssues: ["Rename variable for clarity."],
            PatternIssues: ["Prefer single responsibility."]);

        gitHubDiffService.GetPullRequestDiff(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(diff));
        claudeReviewService.ReviewDiffAsync(diff, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(expected));

        await using var factory = new ReviewAiWebApplicationFactory(gitHubDiffService, claudeReviewService);
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/review/pr",
            new ReviewPullRequestRequest("https://github.com/owner/repo/pull/42"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ReviewPullRequestResult>();
        payload.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task ReviewPullRequest_WhenGitHubReturnsValidationError_ReturnsBadRequestWithoutCallingClaude()
    {
        var gitHubDiffService = Substitute.For<IGitHubDiffService>();
        var claudeReviewService = Substitute.For<IClaudeReviewService>();
        var errorMessage = "Invalid GitHub pull request URL.";

        gitHubDiffService.GetPullRequestDiff(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<string>(new ValidationError(errorMessage)));

        await using var factory = new ReviewAiWebApplicationFactory(gitHubDiffService, claudeReviewService);
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/review/pr",
            new ReviewPullRequestRequest("not-a-pull-request-url"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = await response.Content.ReadFromJsonAsync<string[]>();
        errors.Should().Equal(errorMessage);
        await claudeReviewService.DidNotReceive()
            .ReviewDiffAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReviewPullRequest_WhenGitHubReturnsNotFoundError_ReturnsNotFoundWithoutCallingClaude()
    {
        var gitHubDiffService = Substitute.For<IGitHubDiffService>();
        var claudeReviewService = Substitute.For<IClaudeReviewService>();
        var errorMessage = "Pull request not found.";

        gitHubDiffService.GetPullRequestDiff(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<string>(new NotFoundError(errorMessage)));

        await using var factory = new ReviewAiWebApplicationFactory(gitHubDiffService, claudeReviewService);
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/review/pr",
            new ReviewPullRequestRequest("https://github.com/owner/repo/pull/404"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var errors = await response.Content.ReadFromJsonAsync<string[]>();
        errors.Should().Equal(errorMessage);
        await claudeReviewService.DidNotReceive()
            .ReviewDiffAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReviewPullRequest_WhenGitHubReturnsExternalServiceError_ReturnsBadGatewayWithoutCallingClaude()
    {
        var gitHubDiffService = Substitute.For<IGitHubDiffService>();
        var claudeReviewService = Substitute.For<IClaudeReviewService>();
        var errorMessage = "GitHub service unavailable.";

        gitHubDiffService.GetPullRequestDiff(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<string>(new ExternalServiceError(errorMessage)));

        await using var factory = new ReviewAiWebApplicationFactory(gitHubDiffService, claudeReviewService);
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/review/pr",
            new ReviewPullRequestRequest("https://github.com/owner/repo/pull/42"));

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        var errors = await response.Content.ReadFromJsonAsync<string[]>();
        errors.Should().Equal(errorMessage);
        await claudeReviewService.DidNotReceive()
            .ReviewDiffAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReviewPullRequest_WhenClaudeReturnsInvalidAiResponseError_ReturnsBadGatewayAfterCallingClaude()
    {
        var gitHubDiffService = Substitute.For<IGitHubDiffService>();
        var claudeReviewService = Substitute.For<IClaudeReviewService>();
        var diff = "diff --git a/Program.cs b/Program.cs";
        var errorMessage = "Claude response is invalid.";

        gitHubDiffService.GetPullRequestDiff(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(diff));
        claudeReviewService.ReviewDiffAsync(diff, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<ReviewPullRequestResult>(new InvalidAiResponseError(errorMessage)));

        await using var factory = new ReviewAiWebApplicationFactory(gitHubDiffService, claudeReviewService);
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/review/pr",
            new ReviewPullRequestRequest("https://github.com/owner/repo/pull/42"));

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        var errors = await response.Content.ReadFromJsonAsync<string[]>();
        errors.Should().Equal(errorMessage);
        await claudeReviewService.Received(1)
            .ReviewDiffAsync(diff, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReviewPullRequest_WhenGitHubReturnsGenericError_ReturnsInternalServerErrorWithoutCallingClaude()
    {
        var gitHubDiffService = Substitute.For<IGitHubDiffService>();
        var claudeReviewService = Substitute.For<IClaudeReviewService>();
        var errorMessage = "Unexpected failure.";

        gitHubDiffService.GetPullRequestDiff(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<string>(new Error(errorMessage)));

        await using var factory = new ReviewAiWebApplicationFactory(gitHubDiffService, claudeReviewService);
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/review/pr",
            new ReviewPullRequestRequest("https://github.com/owner/repo/pull/42"));

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var errors = await response.Content.ReadFromJsonAsync<string[]>();
        errors.Should().Equal(errorMessage);
        await claudeReviewService.DidNotReceive()
            .ReviewDiffAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static HttpClient CreateAuthenticatedClient(ReviewAiWebApplicationFactory factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-API-Key", factory.ApiKey);
        return client;
    }
}

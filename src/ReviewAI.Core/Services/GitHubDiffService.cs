using FluentResults;
using Microsoft.Extensions.Logging;
using Octokit;
using ReviewAI.Core.Common.Errors;

namespace ReviewAI.Core.Services;

public sealed class GitHubDiffService(IGitHubClient gitHubClient, ILogger<GitHubDiffService> logger) : IGitHubDiffService
{
    private readonly IGitHubClient _gitHubClient = gitHubClient;
    private readonly ILogger<GitHubDiffService> _logger = logger;

    public async Task<Result<string>> GetPullRequestDiff(string pullRequestUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pullRequestUrl))
        {
            return Result.Fail(new ValidationError("Pull request URL must be provided."));
        }

        if (!Uri.TryCreate(pullRequestUrl, UriKind.Absolute, out var uri))
        {
            return Result.Fail(new ValidationError("Invalid pull request URL."));
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 4 || !string.Equals(segments[2], "pull", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail(new ValidationError("Invalid GitHub pull request URL."));
        }

        var owner = segments[0];
        var repo = segments[1];
        if (!int.TryParse(segments[3], out var pullNumber))
        {
            return Result.Fail(new ValidationError("Invalid pull request number in URL."));
        }

        try
        {
            // Fetch the diff directly from the pull request endpoint using the diff media type.
            // This is a single cancellable call (the CancellationToken is honoured), replacing the
            // earlier metadata + download pair where the metadata call (PullRequest.Get) exposed no
            // CancellationToken overload. GitHub returns the raw diff in the response body.
            var endpoint = new Uri($"repos/{owner}/{repo}/pulls/{pullNumber}", UriKind.Relative);
            var response = await _gitHubClient.Connection.Get<string>(
                endpoint, new Dictionary<string, string>(), "application/vnd.github.v3.diff", cancellationToken);
            return Result.Ok(response.Body ?? string.Empty);
        }
        catch (NotFoundException)
        {
            _logger.LogWarning("Pull request {Owner}/{Repo}#{PullNumber} was not found.", owner, repo, pullNumber);
            return Result.Fail(new NotFoundError("The specified repository or pull request was not found."));
        }
        catch (RateLimitExceededException ex)
        {
            _logger.LogError(ex, "GitHub rate limit exceeded for {Owner}/{Repo}#{PullNumber}; resets at {Reset}.", owner, repo, pullNumber, ex.Reset);
            return Result.Fail(new ExternalServiceError("GitHub rate limit exceeded. Please retry later."));
        }
        catch (SecondaryRateLimitExceededException ex)
        {
            _logger.LogError(ex, "GitHub secondary rate limit hit for {Owner}/{Repo}#{PullNumber}.", owner, repo, pullNumber);
            return Result.Fail(new ExternalServiceError("GitHub secondary rate limit exceeded. Please retry later."));
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "GitHub API request failed for {Owner}/{Repo}#{PullNumber}.", owner, repo, pullNumber);
            return Result.Fail(new ExternalServiceError($"GitHub API request failed: {ex.Message}"));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network failure while contacting GitHub for {Owner}/{Repo}#{PullNumber}.", owner, repo, pullNumber);
            return Result.Fail(new ExternalServiceError($"Network failure while contacting GitHub: {ex.Message}"));
        }
        catch (OperationCanceledException)
        {
            // Honour cancellation semantics (→ 499); never mask it as an external-service failure.
            throw;
        }
        catch (Exception ex)
        {
            // Catch-all for unexpected failures, notably the resilience pipeline's open circuit
            // breaker (Polly's BrokenCircuitException), which would otherwise surface as HTTP 500.
            // Map it to an external-service failure (→ 502) consistent with the Claude path.
            _logger.LogError(ex, "Unexpected failure retrieving diff for {Owner}/{Repo}#{PullNumber}.", owner, repo, pullNumber);
            return Result.Fail(new ExternalServiceError($"Unexpected failure contacting GitHub: {ex.Message}"));
        }
    }
}

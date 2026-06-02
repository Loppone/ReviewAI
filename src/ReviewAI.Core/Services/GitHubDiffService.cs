using FluentResults;
using Octokit;
using ReviewAI.Core.Common.Errors;

namespace ReviewAI.Core.Services;

public sealed class GitHubDiffService(IGitHubClient gitHubClient) : IGitHubDiffService
{
    private readonly IGitHubClient _gitHubClient = gitHubClient;

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
            var pullRequest = await _gitHubClient.Repository.PullRequest.Get(owner, repo, pullNumber);
            if (string.IsNullOrWhiteSpace(pullRequest.DiffUrl))
            {
                return Result.Fail(new ExternalServiceError("Unable to retrieve diff for the specified pull request."));
            }

            var response = await _gitHubClient.Connection.Get<string>(new Uri(pullRequest.DiffUrl), new Dictionary<string, string>(), "application/vnd.github.v3.diff");
            return Result.Ok(response.Body ?? string.Empty);
        }
        catch (NotFoundException)
        {
            return Result.Fail(new NotFoundError("The specified repository or pull request was not found."));
        }
        catch (ApiException ex)
        {
            return Result.Fail(new ExternalServiceError($"GitHub API request failed: {ex.Message}"));
        }
        catch (HttpRequestException ex)
        {
            return Result.Fail(new ExternalServiceError($"Network failure while contacting GitHub: {ex.Message}"));
        }
    }
}

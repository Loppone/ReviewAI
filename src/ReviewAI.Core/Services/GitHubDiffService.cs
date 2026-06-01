using Octokit;

namespace ReviewAI.Core.Services;

public sealed class GitHubDiffService(GitHubClient gitHubClient) : IGitHubDiffService
{
    private readonly GitHubClient _gitHubClient = gitHubClient;

    public async Task<string> GetPullRequestDiff(string pullRequestUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pullRequestUrl))
        {
            throw new ArgumentException("Pull request URL must be provided.", nameof(pullRequestUrl));
        }

        var uri = new Uri(pullRequestUrl);
        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 4 || !string.Equals(segments[2], "pull", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Invalid GitHub pull request URL.", nameof(pullRequestUrl));
        }

        var owner = segments[0];
        var repo = segments[1];
        if (!int.TryParse(segments[3], out var pullNumber))
        {
            throw new ArgumentException("Invalid pull request number in URL.", nameof(pullRequestUrl));
        }

        var pullRequest = await _gitHubClient.Repository.PullRequest.Get(owner, repo, pullNumber);
        if (string.IsNullOrWhiteSpace(pullRequest.DiffUrl))
        {
            throw new InvalidOperationException("Unable to retrieve diff for the specified pull request.");
        }

        var response = await _gitHubClient.Connection.Get<string>(new Uri(pullRequest.DiffUrl), new Dictionary<string, string>(), "application/vnd.github.v3.diff");
        return response.Body ?? string.Empty;
    }
}

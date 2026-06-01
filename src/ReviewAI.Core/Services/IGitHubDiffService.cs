namespace ReviewAI.Core.Services;

public interface IGitHubDiffService
{
    Task<string> GetPullRequestDiff(string pullRequestUrl, CancellationToken cancellationToken);
}

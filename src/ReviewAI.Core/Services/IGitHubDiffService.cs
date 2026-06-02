using FluentResults;

namespace ReviewAI.Core.Services;

public interface IGitHubDiffService
{
    Task<Result<string>> GetPullRequestDiff(string pullRequestUrl, CancellationToken cancellationToken);
}

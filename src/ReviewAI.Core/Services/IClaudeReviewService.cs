using FluentResults;
using ReviewAI.Core.Features.ReviewPullRequest;

namespace ReviewAI.Core.Services;

public interface IClaudeReviewService
{
    Task<Result<ReviewPullRequestResult>> ReviewDiffAsync(string diff, CancellationToken cancellationToken);
}

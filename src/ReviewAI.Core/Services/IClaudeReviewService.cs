using ReviewAI.Core.Features.ReviewPullRequest;

namespace ReviewAI.Core.Services;

public interface IClaudeReviewService
{
    Task<ReviewPullRequestResult> ReviewDiffAsync(string diff, CancellationToken cancellationToken);
}

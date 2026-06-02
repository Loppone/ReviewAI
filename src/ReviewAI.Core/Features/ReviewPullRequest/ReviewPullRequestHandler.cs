using FluentResults;
using MediatR;
using ReviewAI.Core.Services;

namespace ReviewAI.Core.Features.ReviewPullRequest;

public sealed class ReviewPullRequestHandler(
    IGitHubDiffService gitHubDiffService,
    IClaudeReviewService claudeReviewService) : IRequestHandler<ReviewPullRequestCommand, Result<ReviewPullRequestResult>>
{
    private readonly IGitHubDiffService _gitHubDiffService = gitHubDiffService;
    private readonly IClaudeReviewService _claudeReviewService = claudeReviewService;

    public async Task<Result<ReviewPullRequestResult>> Handle(ReviewPullRequestCommand request, CancellationToken cancellationToken)
    {
        var diffResult = await _gitHubDiffService.GetPullRequestDiff(request.PullRequestUrl, cancellationToken);
        if (diffResult.IsFailed)
        {
            return Result.Fail(diffResult.Errors);
        }

        return await _claudeReviewService.ReviewDiffAsync(diffResult.Value, cancellationToken);
    }
}

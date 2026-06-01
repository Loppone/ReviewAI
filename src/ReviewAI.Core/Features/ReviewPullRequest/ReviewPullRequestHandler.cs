using Anthropic;
using MediatR;
using ReviewAI.Core.Services;

namespace ReviewAI.Core.Features.ReviewPullRequest;

public sealed class ReviewPullRequestHandler : IRequestHandler<ReviewPullRequestCommand, ReviewPullRequestResult>
{
    private readonly IGitHubDiffService _gitHubDiffService;
    private readonly IClaudeReviewService _claudeReviewService;

    public ReviewPullRequestHandler(
        IGitHubDiffService gitHubDiffService,
        IClaudeReviewService claudeReviewService)
    {
        _gitHubDiffService = gitHubDiffService;
        _claudeReviewService = claudeReviewService;
    }

    public async Task<ReviewPullRequestResult> Handle(ReviewPullRequestCommand request, CancellationToken cancellationToken)
    {
        var diff = await _gitHubDiffService.GetPullRequestDiff(request.PullRequestUrl, cancellationToken);
        var review = await _claudeReviewService.ReviewDiffAsync(diff, cancellationToken);
        return review;
    }
}

using MediatR;

namespace ReviewAI.Core.Features.ReviewPullRequest;

public sealed record ReviewPullRequestCommand(string PullRequestUrl) : IRequest<ReviewPullRequestResult>;

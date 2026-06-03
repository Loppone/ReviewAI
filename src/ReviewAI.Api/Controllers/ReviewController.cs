using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReviewAI.Api.Http;
using ReviewAI.Core.Features.ReviewPullRequest;

namespace ReviewAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/review")]
public sealed class ReviewController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpPost("pr")]
    public async Task<IActionResult> ReviewPullRequest([FromBody] ReviewPullRequestRequest request, CancellationToken cancellationToken)
    {
        var command = new ReviewPullRequestCommand(request.PullRequestUrl);
        var result = await _mediator.Send(command, cancellationToken);
        return result.ToActionResult();
    }
}

public sealed record ReviewPullRequestRequest(string PullRequestUrl);

using MediatR;
using Microsoft.AspNetCore.Mvc;
using ReviewAI.Core.Features.ReviewPullRequest;

namespace ReviewAI.Api.Controllers;

[ApiController]
[Route("api/review")]
public sealed class ReviewController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReviewController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("pr")]
    public async Task<IActionResult> ReviewPullRequest([FromBody] ReviewPullRequestRequest request, CancellationToken cancellationToken)
    {
        var command = new ReviewPullRequestCommand(request.PullRequestUrl);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}

public sealed record ReviewPullRequestRequest(string PullRequestUrl);

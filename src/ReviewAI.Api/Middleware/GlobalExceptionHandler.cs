using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace ReviewAI.Api.Middleware;

/// <summary>
/// Last-resort handler for genuinely unexpected exceptions that escape the Result
/// pattern. Logs the exception with full context and writes an RFC 7807
/// <see cref="ProblemDetails"/> 500 response (no internal details in the body).
/// </summary>
/// <remarks>
/// Client cancellations are NOT handled here: <c>ExceptionHandlerMiddleware</c>
/// short-circuits <see cref="OperationCanceledException"/>/<see cref="IOException"/>
/// when <c>RequestAborted</c> is signalled (status 499, native abort log) before any
/// <see cref="IExceptionHandler"/> runs.
/// </remarks>
internal sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger = logger;
    private readonly IProblemDetailsService _problemDetailsService = problemDetailsService;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(
            exception,
            "Unhandled exception processing {Method} {Path}.",
            httpContext.Request.Method,
            httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.6.1"
            }
        });
    }
}

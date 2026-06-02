using FluentResults;
using Microsoft.AspNetCore.Mvc;
using ReviewAI.Core.Common.Errors;

namespace ReviewAI.Api.Http;

/// <summary>
/// Translates a FluentResults <see cref="Result{T}"/> into an HTTP <see cref="IActionResult"/>.
/// Keeps HTTP concerns at the API boundary, out of Core handlers and services.
/// </summary>
public static class ResultActionResultExtensions
{
    public static IActionResult ToActionResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return new OkObjectResult(result.Value);
        }

        var messages = result.Errors.Select(error => error.Message).ToArray();

        if (result.HasError<ValidationError>())
        {
            return new BadRequestObjectResult(messages);
        }

        if (result.HasError<NotFoundError>())
        {
            return new NotFoundObjectResult(messages);
        }

        if (result.HasError<ExternalServiceError>() || result.HasError<InvalidAiResponseError>())
        {
            return new ObjectResult(messages) { StatusCode = StatusCodes.Status502BadGateway };
        }

        return new ObjectResult(messages) { StatusCode = StatusCodes.Status500InternalServerError };
    }
}

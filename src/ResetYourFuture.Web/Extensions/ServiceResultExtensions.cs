using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Application.Common;

namespace ResetYourFuture.Web.Extensions;

/// <summary>
/// Maps a <see cref="ServiceResult{T}"/> to the ActionResult a controller should return,
/// replacing the repeated if(!IsSuccess) StatusCode(...) / Ok(Value) pattern.
///
/// Failures are emitted as a single RFC 7807 ProblemDetails envelope (API-1) via
/// <see cref="ControllerBase.Problem"/>, so every ServiceResult error shares the shape — and the
/// configured <c>traceId</c> extension — of bare status results and unhandled exceptions, instead
/// of the raw <c>text/plain</c> string body this used to return. The controller is passed in so the
/// response goes through the app's registered <c>ProblemDetailsFactory</c>.
///
/// Only apply this where the service genuinely puts the failure detail in
/// <see cref="ServiceResult{T}.ErrorMessage"/> — some endpoints (e.g. enrollment) embed the outcome
/// in Value on both success and failure instead, and must shape their own response.
/// </summary>
public static class ServiceResultExtensions
{
    public static ActionResult<T> ToActionResult<T>(this ServiceResult<T> result, ControllerBase controller)
    {
        if (!result.IsSuccess)
            return controller.Problem(detail: result.ErrorMessage, statusCode: result.StatusCode);

        if (result.StatusCode == StatusCodes.Status204NoContent)
            return controller.NoContent();

        return new ObjectResult(result.Value) { StatusCode = result.StatusCode };
    }
}

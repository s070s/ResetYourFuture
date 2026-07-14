using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Application.Common;

namespace ResetYourFuture.Web.Extensions;

/// <summary>
/// Maps a <see cref="ServiceResult{T}"/> to the ActionResult a controller should return.
/// Two conventions exist (CQ-3), each with its own named method here so the choice is
/// visible at the call site instead of an inline <c>StatusCode(...)</c>:
///
/// <list type="bullet">
/// <item><see cref="ToActionResult{T}"/> — the service puts the failure detail in
/// <see cref="ServiceResult{T}.ErrorMessage"/> and <c>Value</c> is meaningful only on success
/// (the majority of services). Failures render as a single RFC 7807 ProblemDetails envelope
/// (API-1) via <see cref="ControllerBase.Problem"/>, so every ServiceResult error shares the
/// shape — and the configured <c>traceId</c> extension — of bare status results and unhandled
/// exceptions.</item>
/// <item><see cref="ToEmbeddedActionResult{T}"/> — the service embeds the outcome in
/// <c>Value</c> on both success and failure (e.g. <c>AuthResponseDto</c>,
/// <c>EnrollmentResultDto</c>): callers rely on the typed DTO's own <c>Success</c>/<c>Message</c>
/// fields being present even on a 4xx response, so it is returned as-is rather than wrapped in
/// ProblemDetails.</item>
/// </list>
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

    public static ActionResult<T> ToEmbeddedActionResult<T>(this ServiceResult<T> result) =>
        new ObjectResult(result.Value) { StatusCode = result.StatusCode };
}

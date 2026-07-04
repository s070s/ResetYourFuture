using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.Common;

namespace ResetYourFuture.Web.Extensions;

/// <summary>
/// Maps a <see cref="ServiceResult{T}"/> to the ActionResult a controller should return,
/// replacing the repeated if(!IsSuccess) StatusCode(...) / Ok(Value) pattern. Only apply
/// this where the service genuinely puts the failure detail in <see cref="ServiceResult{T}.ErrorMessage"/> —
/// some services (e.g. enrollment) embed the outcome in Value on both success and failure instead,
/// which this helper is not a drop-in replacement for.
/// </summary>
public static class ServiceResultExtensions
{
    public static ActionResult<T> ToActionResult<T>(this ServiceResult<T> result)
    {
        if (result.StatusCode == StatusCodes.Status204NoContent)
            return new StatusCodeResult(result.StatusCode);

        return new ObjectResult(result.IsSuccess ? result.Value : result.ErrorMessage)
        {
            StatusCode = result.StatusCode
        };
    }
}

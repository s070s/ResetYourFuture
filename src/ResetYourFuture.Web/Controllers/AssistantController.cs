using System.Runtime.CompilerServices;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.Controllers;

/// <summary>
/// API endpoints for the local AI assistant: a grounded, streaming chat helper available to every
/// authenticated user. See AI_ASSISTANT_PLAN.md for the overall design.
/// </summary>
[ApiController]
[Route("api/assistant")]
[Authorize]
[Tags("AI Assistant")]
public class AssistantController(IAssistantService assistantService) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User ID not found in claims");

    /// <summary>
    /// Streams a grounded answer to the given conversation as Server-Sent Events. Each event's JSON
    /// payload is an AssistantStreamEvent whose Kind is "token" | "sources" | "done" | "error".
    /// </summary>
    [HttpPost("chat")]
    [EnableRateLimiting("assistant")]
    public IResult Chat([FromBody] AssistantChatRequest request, [FromQuery] string lang = "en")
    {
        // SSE tokens must reach the client as they're produced, not once the response buffer fills.
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        return TypedResults.ServerSentEvents(StreamAsync(request, lang, HttpContext.RequestAborted));
    }

    /// <summary>Reports whether the local model backend is reachable, for the widget's offline state.</summary>
    [HttpGet("status")]
    public async Task<ActionResult<AssistantStatusDto>> Status(CancellationToken cancellationToken = default)
    {
        return Ok(await assistantService.GetStatusAsync(cancellationToken));
    }

    private async IAsyncEnumerable<AssistantStreamEvent> StreamAsync(
        AssistantChatRequest request, string lang, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var streamEvent in assistantService.StreamChatAsync(UserId, request, lang, cancellationToken))
            yield return streamEvent;
    }
}

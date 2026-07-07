using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResetYourFuture.Web.Services;

namespace ResetYourFuture.Web.Controllers;

/// <summary>Admin control over the AI assistant's content index.</summary>
[ApiController]
[Route("api/admin/assistant")]
[Authorize(Policy = "AdminOnly")]
[Tags("Admin · AI Assistant")]
public class AdminAssistantController(AssistantIndexSignal signal) : ControllerBase
{
    /// <summary>
    /// Wakes the background indexer to re-index published content immediately instead of waiting
    /// for its next scheduled pass. A no-op if the assistant is disabled or already indexing.
    /// </summary>
    [HttpPost("reindex")]
    public IActionResult Reindex()
    {
        signal.RequestReindex();
        return Accepted();
    }
}

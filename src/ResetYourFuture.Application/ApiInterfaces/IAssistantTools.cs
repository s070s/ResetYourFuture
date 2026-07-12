using Microsoft.Extensions.AI;

namespace ResetYourFuture.Application.ApiInterfaces;

/// <summary>
/// Builds the assistant's tool surface for one authenticated user. The user identity is
/// captured server-side into each tool's closure — no tool accepts or exposes a user id,
/// so prompt injection can never read another user's data. Unauthenticated ⇒ empty list.
/// </summary>
public interface IAssistantTools
{
    IReadOnlyList<AITool> GetToolsForUser(string userId, string language);
}

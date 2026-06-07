using Microsoft.AspNetCore.Components.Web;

namespace ResetYourFuture.Web.Shared;

/// <summary>
/// Helpers for treating non-button elements (with role="button") as keyboard-operable.
/// </summary>
public static class KeyboardEventArgsExtensions
{
    /// <summary>
    /// Returns true when the key press should activate a control (Enter or Space),
    /// matching native button/link activation semantics (WCAG 2.1.1).
    /// </summary>
    public static bool IsActivationKey(this KeyboardEventArgs e) =>
        e.Key is "Enter" or " " or "Spacebar";
}

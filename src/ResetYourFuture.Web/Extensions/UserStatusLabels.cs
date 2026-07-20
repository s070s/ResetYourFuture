using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Shared.Resources;

namespace ResetYourFuture.Web.Extensions;

/// <summary>
/// Localized labels for the user-facing subset of <see cref="UserStatus"/>. <see cref="UserStatus.Unknown"/>
/// is a system default, never a real user choice, so it is intentionally excluded from <see cref="Selectable"/>.
/// </summary>
public static class UserStatusLabels
{
    public static readonly UserStatus[] Selectable =
    [
        UserStatus.Student,
        UserStatus.Graduate,
        UserStatus.NEET,
        UserStatus.Other
    ];

    public static string Label(UserStatus status) => status switch
    {
        UserStatus.Student => GlobalRes.StatusStudent,
        UserStatus.Graduate => GlobalRes.StatusGraduate,
        UserStatus.NEET => GlobalRes.StatusNEET,
        UserStatus.Other => GlobalRes.StatusOther,
        _ => status.ToString()
    };
}

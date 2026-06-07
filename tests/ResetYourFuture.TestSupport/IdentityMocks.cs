using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using ResetYourFuture.Web.Identity;

namespace ResetYourFuture.TestSupport;

/// <summary>
/// Helpers for substituting ASP.NET Identity managers, which have constructor
/// dependencies that make them awkward to mock directly.
/// </summary>
public static class IdentityMocks
{
    /// <summary>
    /// A substitutable <see cref="UserManager{ApplicationUser}"/>. All public members are
    /// virtual, so configure the ones a test needs (e.g. <c>FindByIdAsync</c>,
    /// <c>GetRolesAsync</c>, <c>Users</c>) with NSubstitute.
    /// </summary>
    public static UserManager<ApplicationUser> MockUserManager()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        var options = Options.Create( new IdentityOptions() );

        return Substitute.For<UserManager<ApplicationUser>>(
            store, options, null, null, null, null, null, null, null );
    }

    /// <summary>
    /// A substitutable <see cref="SignInManager{ApplicationUser}"/> over the given user manager.
    /// Configure virtual members (e.g. <c>CheckPasswordSignInAsync</c>) per test.
    /// </summary>
    public static SignInManager<ApplicationUser> MockSignInManager( UserManager<ApplicationUser> userManager )
    {
        return Substitute.For<SignInManager<ApplicationUser>>(
            userManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            Options.Create( new IdentityOptions() ),
            NullLogger<SignInManager<ApplicationUser>>.Instance,
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<ApplicationUser>>() );
    }
}

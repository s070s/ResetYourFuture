# Email Service + Confirmation/Reset Flows Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the stubbed/fail-fast email setup with a real MailKit SMTP `IEmailService`, wire the Blazor `AuthService` register/forgot flows to actually send confirmation/reset links, and add the user-facing `/reset-password` + `/confirm-email` pages and a rate-limited resend-confirmation endpoint with a Login button.

**Architecture:** A new `SmtpEmailService` (MailKit/MimeKit) implements the existing `IEmailService`, configured by `EmailOptions` bound from `Email:Smtp:*`. `Program.cs` registers it whenever `Email:Smtp:Host` is set (dev Papercut or prod), keeps `StubEmailService` as the Development default, and preserves the production fail-fast only when nothing is configured. `AuthService` gains an `IEmailService` dependency and builds absolute links from `App:BaseUrl` (because `HttpContext` is null inside the Blazor circuit), sending inside try/catch so SMTP outages never roll back account creation. Two thin Blazor pages parse `email`/`token`/`userId` from the query string (matching the existing `HttpUtility.ParseQueryString` idiom) and call `IAuthService`. Resend lives on `AuthController` so it gets the existing `"auth"` rate limiter.

**Tech Stack:** .NET 10, Blazor Server (SSR + circuits), ASP.NET Identity, MailKit/MimeKit, xUnit + NSubstitute + Shouldly, `WebApplicationFactory` integration tests, RESX localization (EN + EL).

---

## File Structure

**Create:**
- `src/ResetYourFuture.Infrastructure/ApiServices/EmailOptions.cs` — strongly-typed SMTP options.
- `src/ResetYourFuture.Infrastructure/ApiServices/SmtpEmailService.cs` — MailKit `IEmailService` impl + testable `BuildMessage` seam.
- `src/ResetYourFuture.Web/Pages/ResetPassword.razor` (+ `.cs`) — `/reset-password` page.
- `src/ResetYourFuture.Web/Pages/ConfirmEmail.razor` (+ `.cs`) — `/confirm-email` page.
- `tests/ResetYourFuture.Infrastructure.Tests/SmtpEmailServiceTests.cs` — `BuildMessage` unit tests.

**Modify:**
- `src/ResetYourFuture.Infrastructure/ResetYourFuture.Infrastructure.csproj` — add MailKit.
- `src/ResetYourFuture.Web/Program.cs:240-257` — registration logic + `Configure<EmailOptions>`.
- `src/ResetYourFuture.Web/appsettings.json` — `Email` + `App:BaseUrl` sections.
- `src/ResetYourFuture.Web/appsettings.Development.json` — dev `App:BaseUrl`.
- `src/ResetYourFuture.Infrastructure/Services/AuthService.cs` — inject `IEmailService`, `App:BaseUrl`, send links, `ConfirmEmailAsync`.
- `src/ResetYourFuture.Application/Interfaces/IAuthService.cs` — add `ConfirmEmailAsync`.
- `src/ResetYourFuture.Web/Controllers/AuthController.cs` — add `resend-confirmation`.
- `src/ResetYourFuture.Web/Pages/Login.razor` + `Login.razor.cs` — resend button + handler.
- `src/ResetYourFuture.Shared/Resources/GlobalRes.{resx,el.resx,Designer.cs}` — new UI keys.
- `src/ResetYourFuture.Shared/Resources/Messages/SuccessMessagesRes.{resx,el.resx,Designer.cs}` — `ConfirmationEmailResent`.
- `src/ResetYourFuture.Shared/Resources/Messages/ErrorMessagesRes.{resx,el.resx,Designer.cs}` — `InvalidResetLink`, `InvalidConfirmationLink`.
- `tests/ResetYourFuture.Infrastructure.Tests/AuthServiceTests.cs` — harness + new tests.
- `tests/ResetYourFuture.Web.Tests/AuthControllerTests.cs` — resend tests.

---

## Task 1: MailKit package + EmailOptions + SmtpEmailService

**Files:**
- Modify: `src/ResetYourFuture.Infrastructure/ResetYourFuture.Infrastructure.csproj`
- Create: `src/ResetYourFuture.Infrastructure/ApiServices/EmailOptions.cs`
- Create: `src/ResetYourFuture.Infrastructure/ApiServices/SmtpEmailService.cs`
- Test: `tests/ResetYourFuture.Infrastructure.Tests/SmtpEmailServiceTests.cs`

- [ ] **Step 1: Add the MailKit package**

Run (resolves the latest compatible version and adds a `<PackageReference Include="MailKit" Version="…" />` to the csproj):
```bash
dotnet add src/ResetYourFuture.Infrastructure/ResetYourFuture.Infrastructure.csproj package MailKit
```
Expected: "PackageReference for package 'MailKit' ... added". (MimeKit comes in transitively.)

- [ ] **Step 2: Create `EmailOptions.cs`**

```csharp
namespace ResetYourFuture.Web.ApiServices;

/// <summary>Binds the "Email" configuration section. Secrets (Username/Password) come from
/// User Secrets / environment variables, never committed appsettings.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public SmtpOptions Smtp { get; set; } = new();
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "no-reply@reset-your-future.com";
    public string FromName { get; set; } = "Reset Your Future";
}
```

- [ ] **Step 3: Write the failing test for `BuildMessage`**

Create `tests/ResetYourFuture.Infrastructure.Tests/SmtpEmailServiceTests.cs`:
```csharp
using ResetYourFuture.Web.ApiServices;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Infrastructure.Tests;

public class SmtpEmailServiceTests
{
    private static EmailOptions Options() => new()
    {
        Smtp = new SmtpOptions { FromAddress = "no-reply@ryf.test", FromName = "RYF" }
    };

    [Fact]
    public void BuildMessage_SetsFromToSubjectAndBodies()
    {
        var msg = SmtpEmailService.BuildMessage(
            Options(), "user@example.com", "Confirm your email",
            htmlBody: "<a href=\"https://ryf.test/confirm-email?token=abc\">Confirm</a>",
            textBody: "Confirm: https://ryf.test/confirm-email?token=abc" );

        msg.From.Mailboxes.ShouldHaveSingleItem().Address.ShouldBe( "no-reply@ryf.test" );
        msg.To.Mailboxes.ShouldHaveSingleItem().Address.ShouldBe( "user@example.com" );
        msg.Subject.ShouldBe( "Confirm your email" );
        msg.HtmlBody.ShouldContain( "/confirm-email?token=abc" );
        msg.TextBody.ShouldContain( "/confirm-email?token=abc" );
    }
}
```

- [ ] **Step 4: Run the test to verify it fails to compile**

Run: `dotnet test tests/ResetYourFuture.Infrastructure.Tests --filter "FullyQualifiedName~SmtpEmailServiceTests"`
Expected: BUILD FAILS — `SmtpEmailService` does not exist.

- [ ] **Step 5: Create `SmtpEmailService.cs`**

```csharp
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using ResetYourFuture.Web.ApiInterfaces;

namespace ResetYourFuture.Web.ApiServices;

/// <summary>
/// Sends transactional email over SMTP using MailKit. Provider-agnostic: works against a local
/// Papercut/Mailhog catcher in development and any relay (SES, SendGrid SMTP, Mailgun, O365) in
/// production. Configured via <see cref="EmailOptions"/> (section "Email").
/// </summary>
public sealed class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService( IOptions<EmailOptions> options, ILogger<SmtpEmailService> logger )
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task SendEmailConfirmationAsync( string email, string confirmationLink, CancellationToken cancellationToken = default )
    {
        var html = $"<p>Welcome to Reset Your Future. Please confirm your email address:</p>" +
                   $"<p><a href=\"{confirmationLink}\">Confirm my email</a></p>" +
                   $"<p>If the link does not work, copy this URL into your browser:<br>{confirmationLink}</p>";
        var text = $"Welcome to Reset Your Future. Confirm your email: {confirmationLink}";
        return SendAsync( email, "Confirm your email", html, text, cancellationToken );
    }

    public Task SendPasswordResetAsync( string email, string resetLink, CancellationToken cancellationToken = default )
    {
        var html = $"<p>We received a request to reset your password.</p>" +
                   $"<p><a href=\"{resetLink}\">Reset my password</a></p>" +
                   $"<p>If you did not request this, you can ignore this email. " +
                   $"If the link does not work, copy this URL into your browser:<br>{resetLink}</p>";
        var text = $"Reset your Reset Your Future password: {resetLink}";
        return SendAsync( email, "Reset your password", html, text, cancellationToken );
    }

    private async Task SendAsync( string to, string subject, string htmlBody, string textBody, CancellationToken ct )
    {
        var message = BuildMessage( _options, to, subject, htmlBody, textBody );

        using var client = new SmtpClient();
        var socketOptions = _options.Smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
        await client.ConnectAsync( _options.Smtp.Host, _options.Smtp.Port, socketOptions, ct );
        if ( !string.IsNullOrEmpty( _options.Smtp.Username ) )
            await client.AuthenticateAsync( _options.Smtp.Username, _options.Smtp.Password, ct );
        await client.SendAsync( message, ct );
        await client.DisconnectAsync( true, ct );

        _logger.LogInformation( "Email '{Subject}' sent to {To}.", subject, to );
    }

    /// <summary>Builds the MIME message. Extracted so message construction is unit-testable
    /// without a live SMTP server.</summary>
    public static MimeMessage BuildMessage( EmailOptions options, string to, string subject, string htmlBody, string textBody )
    {
        var message = new MimeMessage();
        message.From.Add( new MailboxAddress( options.Smtp.FromName, options.Smtp.FromAddress ) );
        message.To.Add( MailboxAddress.Parse( to ) );
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody, TextBody = textBody }.ToMessageBody();
        return message;
    }
}
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test tests/ResetYourFuture.Infrastructure.Tests --filter "FullyQualifiedName~SmtpEmailServiceTests"`
Expected: PASS (1 test).

- [ ] **Step 7: Commit**

```bash
git add src/ResetYourFuture.Infrastructure tests/ResetYourFuture.Infrastructure.Tests/SmtpEmailServiceTests.cs
git commit -m "feat(email): add MailKit SmtpEmailService and EmailOptions"
```

---

## Task 2: Register the email service in Program.cs

**Files:**
- Modify: `src/ResetYourFuture.Web/Program.cs:240-257`
- Modify: `src/ResetYourFuture.Web/appsettings.json`
- Modify: `src/ResetYourFuture.Web/appsettings.Development.json`

- [ ] **Step 1: Replace the registration block in `Program.cs`**

Replace this existing block (the NOTE comment + if/else, ~lines 240-257):
```csharp
// NOTE
// StubEmailService only LOGS emails; it never delivers them. The Blazor cookie auth flow
// (AuthService.RegisterAsync / ForgotPasswordAsync) also does not call IEmailService at all,
// so no confirmation/reset email is sent in Development — users self-confirm via the dev-only
// buttons on the Register/Login/ForgotPassword pages. Before production: implement a real
// provider (SendGrid/SMTP), register it here, and wire AuthService to send through it.
if ( builder.Environment.IsDevelopment() )
{
    builder.Services.AddScoped<IEmailService , StubEmailService>();
}
else
{
    // Fail fast: a real email provider must be registered for non-Development environments.
    // Registering StubEmailService in prod would silently swallow all emails.
    throw new InvalidOperationException(
        "No production IEmailService registered. " +
        "Implement and register a real email provider before deploying." );
}
```
with:
```csharp
// Email transport. SmtpEmailService (MailKit) is used whenever Email:Smtp:Host is configured —
// point it at Papercut/Mailhog in Development or a real relay (SES/SendGrid SMTP/etc.) in prod.
// With no SMTP host configured, Development falls back to StubEmailService (logs only); any other
// environment fails fast so emails are never silently swallowed in production.
builder.Services.Configure<EmailOptions>( config.GetSection( EmailOptions.SectionName ) );
if ( !string.IsNullOrWhiteSpace( config [ "Email:Smtp:Host" ] ) )
{
    builder.Services.AddScoped<IEmailService , SmtpEmailService>();
}
else if ( builder.Environment.IsDevelopment() )
{
    builder.Services.AddScoped<IEmailService , StubEmailService>();
}
else
{
    throw new InvalidOperationException(
        "No email transport configured. Set Email:Smtp:Host (and credentials) for production." );
}
```
(`EmailOptions` and `SmtpEmailService` are in `ResetYourFuture.Web.ApiServices`, already imported in Program.cs via the existing `StubEmailService` usage. `config` is the existing `builder.Configuration` alias.)

- [ ] **Step 2: Add `Email` + `App` sections to `appsettings.json`**

Insert after the `"Jwt": { … }` block (keep valid JSON — add a comma after the Jwt block's closing brace):
```jsonc
  "Email": {
    "Smtp": {
      "Host": "",
      "Port": 587,
      "UseStartTls": true,
      "Username": "",
      "Password": "",
      "FromAddress": "no-reply@reset-your-future.com",
      "FromName": "Reset Your Future"
    }
  },
  "App": {
    "BaseUrl": "https://reset-your-future.com"
  },
```

- [ ] **Step 3: Add dev `App:BaseUrl` to `appsettings.Development.json`**

Add an `"App"` section so dev links point at the local origin, not production. Open the file, and add (with a correct trailing/leading comma to keep JSON valid):
```jsonc
  "App": {
    "BaseUrl": "https://localhost:7090"
  }
```
(If the dev HTTPS port differs in `Properties/launchSettings.json`, use that port instead.)

- [ ] **Step 4: Build to verify everything compiles**

Run: `dotnet build src/ResetYourFuture.Web/ResetYourFuture.Web.csproj`
Expected: Build succeeded. (Dev with empty `Email:Smtp:Host` still registers `StubEmailService`, so startup is unaffected.)

- [ ] **Step 5: Commit**

```bash
git add src/ResetYourFuture.Web/Program.cs src/ResetYourFuture.Web/appsettings.json src/ResetYourFuture.Web/appsettings.Development.json
git commit -m "feat(email): register SmtpEmailService when SMTP host configured; add Email/App config"
```

---

## Task 3: Wire AuthService to send links + add ConfirmEmailAsync

**Files:**
- Modify: `src/ResetYourFuture.Application/Interfaces/IAuthService.cs`
- Modify: `src/ResetYourFuture.Infrastructure/Services/AuthService.cs`
- Test: `tests/ResetYourFuture.Infrastructure.Tests/AuthServiceTests.cs`

- [ ] **Step 1: Update the test harness to inject `IEmailService` + `App:BaseUrl`**

In `AuthServiceTests.cs`, add `using ResetYourFuture.Web.ApiInterfaces;` is already present. Change the `Harness` record and `Build()`:

Replace the `Harness` record:
```csharp
    private sealed record Harness(
        AuthService Svc,
        UserManager<ApplicationUser> Um,
        SignInManager<ApplicationUser> Sm,
        ISubscriptionService Subs,
        EphemeralDataProtectionProvider Dp,
        IHttpContextAccessor Accessor );
```
with:
```csharp
    private sealed record Harness(
        AuthService Svc,
        UserManager<ApplicationUser> Um,
        SignInManager<ApplicationUser> Sm,
        ISubscriptionService Subs,
        EphemeralDataProtectionProvider Dp,
        IHttpContextAccessor Accessor,
        IEmailService Email );
```

In `Build()`, add the config key and the substitute, and pass it to the constructor. Replace:
```csharp
        var config = new ConfigurationBuilder().AddInMemoryCollection( new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "test-signing-key-at-least-32-bytes-long-1234567890",
            ["Jwt:Issuer"] = "iss",
            ["Jwt:Audience"] = "aud",
            ["Jwt:AccessTokenExpirationMinutes"] = "60"
        } ).Build();

        var svc = new AuthService( accessor, um, sm, subs, ctx, dp, config, NullLogger<AuthService>.Instance );
        return new Harness( svc, um, sm, subs, dp, accessor );
```
with:
```csharp
        var config = new ConfigurationBuilder().AddInMemoryCollection( new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "test-signing-key-at-least-32-bytes-long-1234567890",
            ["Jwt:Issuer"] = "iss",
            ["Jwt:Audience"] = "aud",
            ["Jwt:AccessTokenExpirationMinutes"] = "60",
            ["App:BaseUrl"] = "https://test.local"
        } ).Build();

        var email = Substitute.For<IEmailService>();
        var svc = new AuthService( accessor, um, sm, subs, email, ctx, dp, config, NullLogger<AuthService>.Instance );
        return new Harness( svc, um, sm, subs, dp, accessor, email );
```

- [ ] **Step 2: Write the new failing tests**

Add these tests to `AuthServiceTests.cs` (inside the class):
```csharp
    // ---- Email sending ------------------------------------------------------

    [Fact]
    public async Task Register_Success_SendsConfirmationEmailWithLink()
    {
        var h = Build();
        h.Um.CreateAsync( Arg.Any<ApplicationUser>(), Arg.Any<string>() ).Returns( IdentityResult.Success );
        h.Um.AddToRoleAsync( Arg.Any<ApplicationUser>(), "Student" ).Returns( IdentityResult.Success );
        h.Um.GenerateEmailConfirmationTokenAsync( Arg.Any<ApplicationUser>() ).Returns( "token" );

        await h.Svc.RegisterAsync( new RegisterRequestDto
        {
            Email = "u@x.com", Password = "Password1", ConfirmPassword = "Password1",
            FirstName = "F", LastName = "L", GdprConsent = true
        } );

        await h.Email.Received( 1 ).SendEmailConfirmationAsync(
            "u@x.com",
            Arg.Is<string>( url => url.Contains( "/confirm-email" ) && url.Contains( "token" ) ),
            Arg.Any<CancellationToken>() );
    }

    [Fact]
    public async Task Register_EmailSendThrows_StillSucceeds()
    {
        var h = Build();
        h.Um.CreateAsync( Arg.Any<ApplicationUser>(), Arg.Any<string>() ).Returns( IdentityResult.Success );
        h.Um.AddToRoleAsync( Arg.Any<ApplicationUser>(), "Student" ).Returns( IdentityResult.Success );
        h.Um.GenerateEmailConfirmationTokenAsync( Arg.Any<ApplicationUser>() ).Returns( "token" );
        h.Email.When( e => e.SendEmailConfirmationAsync( Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>() ) )
               .Do( _ => throw new InvalidOperationException( "smtp down" ) );

        var result = await h.Svc.RegisterAsync( new RegisterRequestDto
        {
            Email = "u@x.com", Password = "Password1", ConfirmPassword = "Password1",
            FirstName = "F", LastName = "L", GdprConsent = true
        } );

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ForgotPassword_ConfirmedUser_SendsResetEmail()
    {
        var h = Build();
        var user = User();
        h.Um.FindByEmailAsync( "u@x.com" ).Returns( user );
        h.Um.IsEmailConfirmedAsync( user ).Returns( true );
        h.Um.GeneratePasswordResetTokenAsync( user ).Returns( "rtoken" );

        var result = await h.Svc.ForgotPasswordAsync( new ForgotPasswordRequestDto { Email = "u@x.com" } );

        result.Success.ShouldBeTrue();
        await h.Email.Received( 1 ).SendPasswordResetAsync(
            "u@x.com",
            Arg.Is<string>( url => url.Contains( "/reset-password" ) && url.Contains( "rtoken" ) ),
            Arg.Any<CancellationToken>() );
    }

    [Fact]
    public async Task ForgotPassword_UnknownUser_DoesNotSendEmail()
    {
        var h = Build();
        h.Um.FindByEmailAsync( "u@x.com" ).Returns( (ApplicationUser?) null );

        await h.Svc.ForgotPasswordAsync( new ForgotPasswordRequestDto { Email = "u@x.com" } );

        await h.Email.DidNotReceive().SendPasswordResetAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>() );
    }

    [Fact]
    public async Task ConfirmEmail_Success_ReturnsSuccess()
    {
        var h = Build();
        var user = User();
        h.Um.FindByIdAsync( "u1" ).Returns( user );
        h.Um.ConfirmEmailAsync( user, "tok" ).Returns( IdentityResult.Success );

        ( await h.Svc.ConfirmEmailAsync( "u1", "tok" ) ).Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ConfirmEmail_BadToken_Fails()
    {
        var h = Build();
        var user = User();
        h.Um.FindByIdAsync( "u1" ).Returns( user );
        h.Um.ConfirmEmailAsync( user, "tok" )
            .Returns( IdentityResult.Failed( new IdentityError { Description = "Invalid token." } ) );

        ( await h.Svc.ConfirmEmailAsync( "u1", "tok" ) ).Success.ShouldBeFalse();
    }
```

- [ ] **Step 3: Run the tests to verify they fail to compile**

Run: `dotnet test tests/ResetYourFuture.Infrastructure.Tests --filter "FullyQualifiedName~AuthServiceTests"`
Expected: BUILD FAILS — `AuthService` constructor has no `IEmailService` parameter; `ConfirmEmailAsync` does not exist.

- [ ] **Step 4: Add `ConfirmEmailAsync` to `IAuthService`**

In `src/ResetYourFuture.Application/Interfaces/IAuthService.cs`, add after the `ResetPasswordAsync` line:
```csharp
    Task<AuthResponseDto> ConfirmEmailAsync( string userId, string token );
```

- [ ] **Step 5: Inject `IEmailService` + `App:BaseUrl` into `AuthService`**

In `AuthService.cs`, add the field (near the other `private readonly` fields):
```csharp
    private readonly IEmailService _emailService;
    private readonly string _appBaseUrl;
```
Add `using ResetYourFuture.Web.ApiInterfaces;` is already present. Update the constructor signature — insert `IEmailService emailService` after `subscriptionService`:
```csharp
    public AuthService(
        IHttpContextAccessor httpContextAccessor ,
        UserManager<ApplicationUser> userManager ,
        SignInManager<ApplicationUser> signInManager ,
        ISubscriptionService subscriptionService ,
        IEmailService emailService ,
        ApplicationDbContext context ,
        IDataProtectionProvider dataProtectionProvider ,
        IConfiguration config ,
        ILogger<AuthService> logger )
```
And in the constructor body, add (after `_subscriptionService = subscriptionService;`):
```csharp
        _emailService = emailService;
        _appBaseUrl = ( config [ "App:BaseUrl" ] ?? "https://localhost:7090" ).TrimEnd( '/' );
```

- [ ] **Step 6: Add link helpers + send in Register/ForgotPassword + ConfirmEmailAsync**

Add these private helpers near the bottom of `AuthService` (e.g. after `CreateSignInToken`):
```csharp
    private string BuildConfirmUrl( string userId, string token ) =>
        $"{_appBaseUrl}/confirm-email?userId={Uri.EscapeDataString( userId )}&token={Uri.EscapeDataString( token )}";

    private string BuildResetUrl( string email, string token ) =>
        $"{_appBaseUrl}/reset-password?email={Uri.EscapeDataString( email )}&token={Uri.EscapeDataString( token )}";
```

In `RegisterAsync`, replace the NOTE block + token line:
```csharp
        // NOTE:
        // The confirmation token is generated but deliberately NOT emailed on this Blazor
        // (cookie) path, which has no IEmailService dependency. In Development users self-confirm
        // via the dev-only button on the Register/Login pages (/api/auth/dev/confirm-email).
        // For production: inject IEmailService, send the confirmation link here, and consolidate
        // with the API path (AuthController.Register already emails) so there is a single flow.
        _ = await _userManager.GenerateEmailConfirmationTokenAsync( user );
        _logger.LogInformation( "User {Email} registered." , request.Email );
```
with:
```csharp
        var confirmToken = await _userManager.GenerateEmailConfirmationTokenAsync( user );
        var confirmUrl = BuildConfirmUrl( user.Id , confirmToken );
        try
        {
            await _emailService.SendEmailConfirmationAsync( user.Email! , confirmUrl );
        }
        catch ( Exception ex )
        {
            // Account creation succeeded; a transient SMTP failure must not roll it back.
            // The user can request a new link via resend-confirmation.
            _logger.LogError( ex , "Failed to send confirmation email to {Email}; account created." , request.Email );
        }
        _logger.LogInformation( "User {Email} registered." , request.Email );
```

In `ForgotPasswordAsync`, replace the NOTE block + token line:
```csharp
        // NOTE:
        // The reset token is generated but NOT emailed here, and there is no /reset-password
        // Blazor page for a link to target. In Development users reset via the dev-only button
        // on the ForgotPassword page (/api/auth/dev/reset-password). For production: send the
        // reset email here and add a /reset-password page.
        _ = await _userManager.GeneratePasswordResetTokenAsync( user );
        _logger.LogInformation( "Password reset requested for {Email}." , request.Email );
```
with:
```csharp
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync( user );
        var resetUrl = BuildResetUrl( user.Email! , resetToken );
        try
        {
            await _emailService.SendPasswordResetAsync( user.Email! , resetUrl );
        }
        catch ( Exception ex )
        {
            _logger.LogError( ex , "Failed to send password reset email to {Email}." , request.Email );
        }
        _logger.LogInformation( "Password reset requested for {Email}." , request.Email );
```

Add the new `ConfirmEmailAsync` method (e.g. right after `ResetPasswordAsync`):
```csharp
    public async Task<AuthResponseDto> ConfirmEmailAsync( string userId, string token )
    {
        if ( string.IsNullOrEmpty( userId ) || string.IsNullOrEmpty( token ) )
            return new AuthResponseDto { Success = false , Message = "Invalid confirmation link." };

        var user = await _userManager.FindByIdAsync( userId );
        if ( user is null )
            return new AuthResponseDto { Success = false , Message = "Invalid confirmation link." };

        var result = await _userManager.ConfirmEmailAsync( user , token );
        if ( !result.Succeeded )
        {
            _logger.LogWarning( "Email confirmation failed for {UserId}: {Errors}" ,
                userId , string.Join( ", " , result.Errors.Select( e => e.Description ) ) );
            return new AuthResponseDto { Success = false , Errors = result.Errors.Select( e => e.Description ) };
        }

        _logger.LogInformation( "Email confirmed for user {UserId}" , userId );
        return new AuthResponseDto { Success = true , Message = "Email confirmed successfully." };
    }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/ResetYourFuture.Infrastructure.Tests --filter "FullyQualifiedName~AuthServiceTests"`
Expected: PASS (all existing + 6 new).

- [ ] **Step 8: Commit**

```bash
git add src/ResetYourFuture.Application/Interfaces/IAuthService.cs src/ResetYourFuture.Infrastructure/Services/AuthService.cs tests/ResetYourFuture.Infrastructure.Tests/AuthServiceTests.cs
git commit -m "feat(auth): send confirmation/reset emails from AuthService; add ConfirmEmailAsync"
```

---

## Task 4: Add localization resource keys

**Files:**
- Modify: `src/ResetYourFuture.Shared/Resources/GlobalRes.resx`, `.el.resx`, `.Designer.cs`
- Modify: `src/ResetYourFuture.Shared/Resources/Messages/SuccessMessagesRes.resx`, `.el.resx`, `.Designer.cs`
- Modify: `src/ResetYourFuture.Shared/Resources/Messages/ErrorMessagesRes.resx`, `.el.resx`, `.Designer.cs`

Each new key needs three edits: English `.resx`, Greek `.el.resx`, and a property in `.Designer.cs`. Existing entries (e.g. `ForgotPasswordTitle` at `GlobalRes.resx:159`) show the exact format.

- [ ] **Step 1: GlobalRes — English `.resx`**

Add inside `<root>` (anywhere among the `<data>` entries) in `GlobalRes.resx`:
```xml
  <data name="ResetPasswordTitle" xml:space="preserve">
    <value>Reset Password</value>
  </data>
  <data name="Label_ResetPassword" xml:space="preserve">
    <value>Reset Password</value>
  </data>
  <data name="ConfirmEmailTitle" xml:space="preserve">
    <value>Confirm Email</value>
  </data>
  <data name="ConfirmingEmail" xml:space="preserve">
    <value>Confirming your email…</value>
  </data>
  <data name="Label_ResendConfirmation" xml:space="preserve">
    <value>Resend confirmation email</value>
  </data>
```

- [ ] **Step 2: GlobalRes — Greek `.el.resx`**

Add the same keys to `GlobalRes.el.resx`:
```xml
  <data name="ResetPasswordTitle" xml:space="preserve">
    <value>Επαναφορά Κωδικού</value>
  </data>
  <data name="Label_ResetPassword" xml:space="preserve">
    <value>Επαναφορά Κωδικού</value>
  </data>
  <data name="ConfirmEmailTitle" xml:space="preserve">
    <value>Επιβεβαίωση Email</value>
  </data>
  <data name="ConfirmingEmail" xml:space="preserve">
    <value>Επιβεβαίωση του email σας…</value>
  </data>
  <data name="Label_ResendConfirmation" xml:space="preserve">
    <value>Επαναποστολή email επιβεβαίωσης</value>
  </data>
```

- [ ] **Step 3: GlobalRes — `.Designer.cs` properties**

Add these properties inside the `GlobalRes` class in `GlobalRes.Designer.cs` (matching the existing property style, e.g. after the `ForgotPasswordTitle` property):
```csharp
        public static string ResetPasswordTitle {
            get {
                return ResourceManager.GetString("ResetPasswordTitle", resourceCulture);
            }
        }

        public static string Label_ResetPassword {
            get {
                return ResourceManager.GetString("Label_ResetPassword", resourceCulture);
            }
        }

        public static string ConfirmEmailTitle {
            get {
                return ResourceManager.GetString("ConfirmEmailTitle", resourceCulture);
            }
        }

        public static string ConfirmingEmail {
            get {
                return ResourceManager.GetString("ConfirmingEmail", resourceCulture);
            }
        }

        public static string Label_ResendConfirmation {
            get {
                return ResourceManager.GetString("Label_ResendConfirmation", resourceCulture);
            }
        }
```

- [ ] **Step 4: SuccessMessagesRes — all three files**

`SuccessMessagesRes.resx`:
```xml
  <data name="ConfirmationEmailResent" xml:space="preserve">
    <value>If your email is registered and not yet confirmed, a new confirmation link has been sent.</value>
  </data>
```
`SuccessMessagesRes.el.resx`:
```xml
  <data name="ConfirmationEmailResent" xml:space="preserve">
    <value>Αν το email σας είναι καταχωρημένο και δεν έχει επιβεβαιωθεί, στάλθηκε νέος σύνδεσμος επιβεβαίωσης.</value>
  </data>
```
`SuccessMessagesRes.Designer.cs` (inside the class):
```csharp
        public static string ConfirmationEmailResent {
            get {
                return ResourceManager.GetString("ConfirmationEmailResent", resourceCulture);
            }
        }
```

- [ ] **Step 5: ErrorMessagesRes — all three files**

`ErrorMessagesRes.resx`:
```xml
  <data name="InvalidResetLink" xml:space="preserve">
    <value>This password reset link is invalid or has expired. Please request a new one.</value>
  </data>
  <data name="InvalidConfirmationLink" xml:space="preserve">
    <value>This confirmation link is invalid or has expired.</value>
  </data>
```
`ErrorMessagesRes.el.resx`:
```xml
  <data name="InvalidResetLink" xml:space="preserve">
    <value>Αυτός ο σύνδεσμος επαναφοράς κωδικού δεν είναι έγκυρος ή έχει λήξει. Ζητήστε νέο.</value>
  </data>
  <data name="InvalidConfirmationLink" xml:space="preserve">
    <value>Αυτός ο σύνδεσμος επιβεβαίωσης δεν είναι έγκυρος ή έχει λήξει.</value>
  </data>
```
`ErrorMessagesRes.Designer.cs` (inside the class):
```csharp
        public static string InvalidResetLink {
            get {
                return ResourceManager.GetString("InvalidResetLink", resourceCulture);
            }
        }

        public static string InvalidConfirmationLink {
            get {
                return ResourceManager.GetString("InvalidConfirmationLink", resourceCulture);
            }
        }
```

- [ ] **Step 6: Build to verify resources compile and resolve**

Run: `dotnet build src/ResetYourFuture.Shared/ResetYourFuture.Shared.csproj`
Expected: Build succeeded.

- [ ] **Step 7: Commit**

```bash
git add src/ResetYourFuture.Shared/Resources
git commit -m "feat(i18n): add reset/confirm/resend resource strings (EN + EL)"
```

---

## Task 5: `/reset-password` Blazor page

**Files:**
- Create: `src/ResetYourFuture.Web/Pages/ResetPassword.razor`
- Create: `src/ResetYourFuture.Web/Pages/ResetPassword.razor.cs`

- [ ] **Step 1: Create `ResetPassword.razor`**

```razor
@page "/reset-password"

<PageTitle>@GlobalRes.ResetPasswordTitle</PageTitle>

<div class="auth-container">
    <h2>@GlobalRes.ResetPasswordTitle</h2>

    @if (linkInvalid)
    {
        <div class="alert alert-danger">@ErrorMessagesRes.InvalidResetLink</div>
        <p class="mt-3"><a href="/forgot-password">@GlobalRes.Label_ForgotPassword</a></p>
    }
    else if (!string.IsNullOrEmpty(successMessage))
    {
        <div class="alert alert-success">
            @successMessage
            <div class="mt-2"><a href="/login">@ErrorMessagesRes.BackToLogin</a></div>
        </div>
    }
    else
    {
        @if (!string.IsNullOrEmpty(errorMessage))
        {
            <div class="alert alert-danger">@errorMessage</div>
        }

        <EditForm Model="resetRequest" OnValidSubmit="HandleSubmit">
            <DataAnnotationsValidator />

            <div class="mb-3">
                <label for="newPassword" class="form-label">@GlobalRes.Label_NewPassword</label>
                <InputText id="newPassword" type="password" class="form-control" @bind-Value="resetRequest.NewPassword" />
                <ValidationMessage For="@(() => resetRequest.NewPassword)" />
            </div>

            <div class="mb-3">
                <label for="confirmPassword" class="form-label">@GlobalRes.Label_ConfirmPassword</label>
                <InputText id="confirmPassword" type="password" class="form-control" @bind-Value="resetRequest.ConfirmPassword" />
                <ValidationMessage For="@(() => resetRequest.ConfirmPassword)" />
            </div>

            <button type="submit" class="btn btn-primary" disabled="@isLoading">
                @if (isLoading) { <span>@GlobalRes.Label_Sending</span> } else { <span>@GlobalRes.Label_ResetPassword</span> }
            </button>
        </EditForm>

        <p class="mt-3"><a href="/login">@ErrorMessagesRes.BackToLogin</a></p>
    }
</div>
```

- [ ] **Step 2: Create `ResetPassword.razor.cs`**

```csharp
using Microsoft.AspNetCore.Components;
using ResetYourFuture.Web.Interfaces;
using ResetYourFuture.Shared.DTOs;
using System.Web;

namespace ResetYourFuture.Web.Pages;

public partial class ResetPassword
{
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private ResetPasswordRequestDto resetRequest = new();
    private string? successMessage;
    private string? errorMessage;
    private bool isLoading;
    private bool linkInvalid;

    protected override void OnInitialized()
    {
        var uri = Navigation.ToAbsoluteUri( Navigation.Uri );
        var query = HttpUtility.ParseQueryString( uri.Query );
        var email = query[ "email" ];
        var token = query[ "token" ];

        if ( string.IsNullOrEmpty( email ) || string.IsNullOrEmpty( token ) )
        {
            linkInvalid = true;
            return;
        }

        resetRequest.Email = email;
        resetRequest.Token = token;
    }

    private async Task HandleSubmit()
    {
        isLoading = true;
        errorMessage = null;
        try
        {
            var result = await AuthService.ResetPasswordAsync( resetRequest );
            if ( result.Success )
                successMessage = result.Message;
            else
                errorMessage = result.Errors is not null ? string.Join( " ", result.Errors ) : result.Message;
        }
        catch ( Exception ex )
        {
            errorMessage = ex.Message;
        }
        finally
        {
            isLoading = false;
        }
    }
}
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build src/ResetYourFuture.Web/ResetYourFuture.Web.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/ResetYourFuture.Web/Pages/ResetPassword.razor src/ResetYourFuture.Web/Pages/ResetPassword.razor.cs
git commit -m "feat(auth): add /reset-password page"
```

---

## Task 6: `/confirm-email` Blazor page

**Files:**
- Create: `src/ResetYourFuture.Web/Pages/ConfirmEmail.razor`
- Create: `src/ResetYourFuture.Web/Pages/ConfirmEmail.razor.cs`

- [ ] **Step 1: Create `ConfirmEmail.razor`**

```razor
@page "/confirm-email"

<PageTitle>@GlobalRes.ConfirmEmailTitle</PageTitle>

<div class="auth-container">
    <h2>@GlobalRes.ConfirmEmailTitle</h2>

    @if (isConfirming)
    {
        <p>@GlobalRes.ConfirmingEmail</p>
    }
    else if (succeeded)
    {
        <div class="alert alert-success">
            @SuccessMessagesRes.EmailConfirmationSuccess
            <div class="mt-2"><a href="/login">@GlobalRes.Label_Login</a></div>
        </div>
    }
    else
    {
        <div class="alert alert-danger">@errorMessage</div>
        <p class="mt-3"><a href="/login">@ErrorMessagesRes.BackToLogin</a></p>
    }
</div>
```

- [ ] **Step 2: Create `ConfirmEmail.razor.cs`**

```csharp
using Microsoft.AspNetCore.Components;
using ResetYourFuture.Web.Interfaces;
using ResetYourFuture.Shared.Resources.Messages;
using System.Web;

namespace ResetYourFuture.Web.Pages;

public partial class ConfirmEmail
{
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private bool isConfirming = true;
    private bool succeeded;
    private string? errorMessage;

    protected override async Task OnInitializedAsync()
    {
        var uri = Navigation.ToAbsoluteUri( Navigation.Uri );
        var query = HttpUtility.ParseQueryString( uri.Query );
        var userId = query[ "userId" ];
        var token = query[ "token" ];

        if ( string.IsNullOrEmpty( userId ) || string.IsNullOrEmpty( token ) )
        {
            isConfirming = false;
            errorMessage = ErrorMessagesRes.InvalidConfirmationLink;
            return;
        }

        var result = await AuthService.ConfirmEmailAsync( userId, token );
        isConfirming = false;
        succeeded = result.Success;
        if ( !result.Success )
            errorMessage = ErrorMessagesRes.EmailConfirmationError;
    }
}
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build src/ResetYourFuture.Web/ResetYourFuture.Web.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/ResetYourFuture.Web/Pages/ConfirmEmail.razor src/ResetYourFuture.Web/Pages/ConfirmEmail.razor.cs
git commit -m "feat(auth): add /confirm-email page"
```

---

## Task 7: Resend-confirmation endpoint

**Files:**
- Modify: `src/ResetYourFuture.Web/Controllers/AuthController.cs`
- Test: `tests/ResetYourFuture.Web.Tests/AuthControllerTests.cs`

- [ ] **Step 1: Write the failing integration tests**

Add to `AuthControllerTests.cs`. First extend the using block at the top so the substitute test compiles:
```csharp
using System.Threading;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using ResetYourFuture.Web.ApiInterfaces;
using ResetYourFuture.Web.Identity;
```
Then add the tests inside the class:
```csharp
    [Fact]
    public async Task ResendConfirmation_UnknownEmail_Returns200Generic()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/resend-confirmation", $"nobody-{Guid.NewGuid():N}@test.com" );

        response.StatusCode.ShouldBe( HttpStatusCode.OK );
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        body!.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ResendConfirmation_UnconfirmedUser_SendsEmail()
    {
        var email = $"unconf-{Guid.NewGuid():N}@test.com";
        var emailSub = Substitute.For<IEmailService>();

        // Separate server instance (own DI + own rate limiter) with IEmailService swapped for a spy.
        using var factory = _factory.WithWebHostBuilder( b =>
            b.ConfigureTestServices( services =>
            {
                services.RemoveAll<IEmailService>();
                services.AddScoped( _ => emailSub );
            } ) );

        using ( var scope = factory.Services.CreateScope() )
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email, Email = email, FirstName = "U", LastName = "C",
                EmailConfirmed = false, IsEnabled = true
            };
            await um.CreateAsync( user, CustomWebAppFactory.TestPassword );
        }

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync( "/api/auth/resend-confirmation", email );

        response.StatusCode.ShouldBe( HttpStatusCode.OK );
        await emailSub.Received( 1 ).SendEmailConfirmationAsync(
            email, Arg.Any<string>(), Arg.Any<CancellationToken>() );
    }

    [Fact]
    public async Task ResendConfirmation_ConfirmedUser_DoesNotSendEmail()
    {
        var email = $"conf-{Guid.NewGuid():N}@test.com";
        var emailSub = Substitute.For<IEmailService>();

        using var factory = _factory.WithWebHostBuilder( b =>
            b.ConfigureTestServices( services =>
            {
                services.RemoveAll<IEmailService>();
                services.AddScoped( _ => emailSub );
            } ) );

        using ( var scope = factory.Services.CreateScope() )
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email, Email = email, FirstName = "U", LastName = "C",
                EmailConfirmed = true, IsEnabled = true
            };
            await um.CreateAsync( user, CustomWebAppFactory.TestPassword );
        }

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync( "/api/auth/resend-confirmation", email );

        response.StatusCode.ShouldBe( HttpStatusCode.OK );
        await emailSub.DidNotReceive().SendEmailConfirmationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>() );
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/ResetYourFuture.Web.Tests --filter "FullyQualifiedName~ResendConfirmation"`
Expected: FAIL — endpoint returns 404 (route not mapped) / assertions fail.

- [ ] **Step 3: Add the endpoint to `AuthController`**

Insert after the `ResetPassword` action (and before the `GetCurrentUser`/`#if DEBUG` section):
```csharp
    /// <summary>
    /// Resend the email-confirmation link to an unconfirmed account. Always returns a generic
    /// response so callers cannot probe which addresses are registered or already confirmed.
    /// </summary>
    [HttpPost( "resend-confirmation" )]
    [EnableRateLimiting( "auth" )]
    public async Task<ActionResult<AuthResponseDto>> ResendConfirmation( [FromBody] string email )
    {
        var generic = new AuthResponseDto
        {
            Success = true,
            Message = "If an account with that email exists and is unconfirmed, a new confirmation link has been sent."
        };

        if ( string.IsNullOrWhiteSpace( email ) )
            return Ok( generic );

        var user = await _userManager.FindByEmailAsync( email );
        if ( user is null || await _userManager.IsEmailConfirmedAsync( user ) )
            return Ok( generic );

        var confirmToken = await _userManager.GenerateEmailConfirmationTokenAsync( user );
        var confirmUrl = $"{Request.Scheme}://{Request.Host}/confirm-email" +
                         $"?userId={Uri.EscapeDataString( user.Id )}&token={Uri.EscapeDataString( confirmToken )}";

        try
        {
            await _emailService.SendEmailConfirmationAsync( user.Email!, confirmUrl );
            _logger.LogInformation( "Resent confirmation email to {Email}." , email );
        }
        catch ( Exception ex )
        {
            _logger.LogError( ex , "Failed to resend confirmation email to {Email}." , email );
        }

        return Ok( generic );
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/ResetYourFuture.Web.Tests --filter "FullyQualifiedName~ResendConfirmation"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ResetYourFuture.Web/Controllers/AuthController.cs tests/ResetYourFuture.Web.Tests/AuthControllerTests.cs
git commit -m "feat(auth): add rate-limited resend-confirmation endpoint"
```

---

## Task 8: Resend button on the Login page

**Files:**
- Modify: `src/ResetYourFuture.Web/Pages/Login.razor`
- Modify: `src/ResetYourFuture.Web/Pages/Login.razor.cs`

- [ ] **Step 1: Add the resend button to `Login.razor`**

Replace the existing `unconfirmedEmailPending` block:
```razor
    @if (unconfirmedEmailPending)
    {
        <div class="alert alert-warning">@ErrorMessagesRes.EmailNotConfirmedHint</div>
        @if (Env.IsDevelopment())
        {
            <div class="mt-2">
                <button class="btn btn-sm btn-warning" @onclick="DevConfirmPendingEmail">@GlobalRes.Label_ConfirmEmail</button>
            </div>
        }
    }
```
with:
```razor
    @if (unconfirmedEmailPending)
    {
        <div class="alert alert-warning">@ErrorMessagesRes.EmailNotConfirmedHint</div>
        <div class="mt-2">
            <button class="btn btn-sm btn-primary" @onclick="ResendConfirmation" disabled="@isResending">
                @if (isResending) { <span>@GlobalRes.Label_Sending</span> } else { <span>@GlobalRes.Label_ResendConfirmation</span> }
            </button>
        </div>
        @if (Env.IsDevelopment())
        {
            <div class="mt-2">
                <button class="btn btn-sm btn-warning" @onclick="DevConfirmPendingEmail">@GlobalRes.Label_ConfirmEmail</button>
            </div>
        }
    }
```

- [ ] **Step 2: Add the handler to `Login.razor.cs`**

Add the field next to the other private fields:
```csharp
    private bool isResending;
```
Add the method (e.g. after `DevConfirmPendingEmail`):
```csharp
    private async Task ResendConfirmation()
    {
        if ( string.IsNullOrEmpty( pendingUnconfirmedEmail ) )
            return;

        isResending = true;
        errorMessage = null;
        devSuccessMessage = null;
        try
        {
            var http = HttpClientFactory.CreateClient( "SelfClient" );
            var response = await http.PostAsJsonAsync( "api/auth/resend-confirmation" , pendingUnconfirmedEmail );

            if ( response.IsSuccessStatusCode )
                devSuccessMessage = SuccessMessagesRes.ConfirmationEmailResent;
            else
                errorMessage = ErrorMessagesRes.EmailConfirmationError;
        }
        catch ( Exception ex )
        {
            errorMessage = $"{ErrorMessagesRes.EmailConfirmationError}: {ex.Message}";
        }
        finally
        {
            isResending = false;
        }
    }
```
(`devSuccessMessage` is the existing top-of-page success banner; reusing it shows the generic "sent" confirmation. `SuccessMessagesRes` and `ErrorMessagesRes` are already imported in this file.)

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build src/ResetYourFuture.Web/ResetYourFuture.Web.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/ResetYourFuture.Web/Pages/Login.razor src/ResetYourFuture.Web/Pages/Login.razor.cs
git commit -m "feat(auth): add resend-confirmation button on Login page"
```

---

## Task 9: Full verification

**Files:** none (verification only)

- [ ] **Step 1: Full solution build**

Run: `dotnet build ResetYourFuture.sln`
Expected: Build succeeded, 0 errors.

- [ ] **Step 2: Full test suite**

Run: `dotnet test ResetYourFuture.sln`
Expected: All tests pass (including the new `SmtpEmailServiceTests`, `AuthServiceTests`, and resend `AuthControllerTests`).

- [ ] **Step 3: Manual live-send check against Papercut/Mailhog**

Start a local SMTP catcher (Papercut SMTP on `localhost:25`, or Mailhog on `localhost:1025`). Configure User Secrets for the Web project so the real SMTP service is selected in Development:
```bash
cd src/ResetYourFuture.Web
dotnet user-secrets set "Email:Smtp:Host" "localhost"
dotnet user-secrets set "Email:Smtp:Port" "25"        # Mailhog: 1025
dotnet user-secrets set "Email:Smtp:UseStartTls" "false"
```
Run the app (`dotnet run --project src/ResetYourFuture.Web`). Then:
1. Register a new user → a "Confirm your email" message appears in the catcher; the link opens `/confirm-email` and confirms successfully.
2. Try to log in before confirming → the unconfirmed-email warning shows a "Resend confirmation email" button; clicking it produces a new message in the catcher.
3. Use Forgot Password for a confirmed user → a "Reset your password" message appears; the link opens `/reset-password`, and submitting a new password succeeds and allows login.

- [ ] **Step 4: Final commit (if any working-tree changes remain, e.g. user-secrets are NOT committed)**

Confirm no secrets are staged:
```bash
git status
```
Expected: clean working tree (User Secrets live outside the repo and must never be committed).

---

## Notes for the implementer

- **Do not commit SMTP credentials.** `appsettings.json` ships empty placeholders; real values come from User Secrets / environment variables only.
- **Two auth paths exist by design.** This plan wires the Blazor `AuthService` (cookie) path. `AuthController.Register`/`ForgotPassword` already send via `IEmailService` for API consumers and are intentionally left as-is (out of scope).
- **`config` in Program.cs** is the existing `builder.Configuration` alias used throughout that file — do not introduce a new variable.
- If the dev HTTPS port is not `7090`, set `App:BaseUrl` in `appsettings.Development.json` (and the user-secret-free default) to the actual port from `launchSettings.json`.

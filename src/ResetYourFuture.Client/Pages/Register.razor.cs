using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ResetYourFuture.Client.Interfaces;
using ResetYourFuture.Shared.DTOs;
using ResetYourFuture.Shared.Resources.Messages;
using System.Net.Http.Json;

namespace ResetYourFuture.Client.Pages;

public partial class Register : IDisposable
{
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IWebAssemblyHostEnvironment Env { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;

    // RegisterRequest.DateOfBirth is now DateTime? default to 2000-01-01 as requested
    private RegisterRequestDto registerRequest = new() { DateOfBirth = new DateTime( 2000 , 1 , 1 ) };
    private EditContext editContext = default!;
    private string? successMessage;
    private string? errorMessage;
    private List<string> errors = new();
    private bool isLoading;
    private bool showPassword;
    private bool showConfirmPassword;
    private string registeredEmail = string.Empty;

    private string passwordInputType => showPassword ? "text" : "password";
    private string passwordIconClass => showPassword ? "fa-solid fa-eye-slash" : "fa-solid fa-eye";
    private string confirmPasswordInputType => showConfirmPassword ? "text" : "password";
    private string confirmPasswordIconClass => showConfirmPassword ? "fa-solid fa-eye-slash" : "fa-solid fa-eye";

    protected override void OnInitialized()
    {
        editContext = new EditContext( registerRequest );
        editContext.OnFieldChanged += HandleFieldChanged;
    }

    private void HandleFieldChanged( object? sender , FieldChangedEventArgs e )
    {
        successMessage = null;
        errorMessage = null;
        errors.Clear();

        if ( e.FieldIdentifier.FieldName == nameof( RegisterRequestDto.Password ) )
        {
            editContext.NotifyFieldChanged( editContext.Field( nameof( RegisterRequestDto.ConfirmPassword ) ) );
        }
    }

    public void Dispose()
    {
        editContext.OnFieldChanged -= HandleFieldChanged;
    }

    private void TogglePasswordVisibility()
    {
        showPassword = !showPassword;
    }

    private void ToggleConfirmPasswordVisibility()
    {
        showConfirmPassword = !showConfirmPassword;
    }

    private async Task HandleRegister()
    {
        isLoading = true;
        successMessage = null;
        errorMessage = null;
        errors.Clear();

        // Trim all text inputs
        registerRequest.Email = registerRequest.Email?.Trim() ?? string.Empty;
        registerRequest.Password = registerRequest.Password?.Trim() ?? string.Empty;
        registerRequest.ConfirmPassword = registerRequest.ConfirmPassword?.Trim() ?? string.Empty;
        registerRequest.FirstName = registerRequest.FirstName?.Trim() ?? string.Empty;
        registerRequest.LastName = registerRequest.LastName?.Trim() ?? string.Empty;

        registeredEmail = registerRequest.Email;
        var result = await AuthService.RegisterAsync( registerRequest );

        if ( result.Success )
        {
            successMessage = result.Message ?? SuccessMessagesRes.RegistrationSuccess;
            registerRequest = new()
            {
                DateOfBirth = new DateTime( 2000 , 1 , 1 )
            }; // Reset, keep default DOB
            editContext.OnFieldChanged -= HandleFieldChanged;
            editContext = new EditContext( registerRequest );
            editContext.OnFieldChanged += HandleFieldChanged;
        }
        else
        {
            errorMessage = result.Message;
            if ( result.Errors != null )
            {
                // Filter out Identity errors that mention username/UserName because the UI does not expose a username field
                // and usernames are set to the email server-side. Showing username errors is confusing to users.
                errors = result.Errors
                    .Where( e => !( e?.Contains( "username" , StringComparison.OrdinalIgnoreCase ) == true
                                  || e?.Contains( "user name" , StringComparison.OrdinalIgnoreCase ) == true
                                  || e?.Contains( "UserName" , StringComparison.OrdinalIgnoreCase ) == true ) )
                    .ToList();

                if ( errors.Any( e => e.Contains( "already taken" ) || e.Contains( "duplicate" ) || e.Contains( "exists" ) ) )
                {
                    errorMessage = ErrorMessagesRes.EmailTakenError;
                }
            }
        }

        isLoading = false;
    }

    private async Task DevConfirmEmail()
    {
        if ( string.IsNullOrEmpty( registeredEmail ) )
            return;

        try
        {
            var response = await Http.PostAsJsonAsync( "api/auth/dev/confirm-email" , registeredEmail );
            if ( response.IsSuccessStatusCode )
            {
                successMessage = SuccessMessagesRes.EmailConfirmationSuccess;
            }
            else
            {
                errorMessage = ErrorMessagesRes.EmailConfirmationError;
            }
        }
        catch ( Exception ex )
        {
            errorMessage = $"{ErrorMessagesRes.Error}: {ex.Message}";
        }
    }
}

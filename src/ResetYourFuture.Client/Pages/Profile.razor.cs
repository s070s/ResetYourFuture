using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using ResetYourFuture.Client.Interfaces;
using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Client.Pages;

public partial class Profile
{
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private ProfileDto? profile;
    private bool _isImpersonating;
    private string displayName = string.Empty;
    private string currentPassword = string.Empty;
    private string newPassword = string.Empty;
    private string confirmPassword = string.Empty;
    private bool isSaving = false;
    private bool isChangingPassword = false;
    private string message = string.Empty;
    private string? avatarDataUrl;

    protected override async Task OnInitializedAsync()
    {
        _isImpersonating = await AuthService.IsImpersonatingAsync();
        try
        {
            profile = await Http.GetFromJsonAsync<ProfileDto>( "api/profile" );
            if ( profile is not null )
            {
                displayName = profile.DisplayName ?? string.Empty;
                await LoadAvatarAsync();
            }
        }
        catch
        {
            Navigation.NavigateTo( "/login" );
        }
    }

    private async Task LoadAvatarAsync()
    {
        if ( profile is null || string.IsNullOrEmpty( profile.AvatarPath ) )
        {
            avatarDataUrl = null;
            return;
        }

        try
        {
            var response = await Http.GetAsync( "api/profile/avatar" );
            if ( response.IsSuccessStatusCode )
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/png";
                avatarDataUrl = $"data:{contentType};base64,{Convert.ToBase64String( bytes )}";
            }
        }
        catch
        {
            avatarDataUrl = null;
        }
    }

    private async Task HandleAvatarUpload( InputFileChangeEventArgs e )
    {
        var file = e.File;
        if ( file == null || file.Size >= 5 * 1024 * 1024 )
        {
            message = "File too large (max 5MB)";
            return;
        }

        try
        {
            const long maxFileSize = 5 * 1024 * 1024;
            using var content = new MultipartFormDataContent();
            using var stream = file.OpenReadStream( maxFileSize );
            var streamContent = new StreamContent( stream );
            streamContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue( file.ContentType );
            content.Add( streamContent , "file" , file.Name );

            var response = await Http.PostAsync( "api/profile/avatar" , content );
            if ( response.IsSuccessStatusCode )
            {
                profile = await Http.GetFromJsonAsync<ProfileDto>( "api/profile" );
                await LoadAvatarAsync();
                message = "Avatar uploaded successfully";
            }
            else
            {
                message = "Error uploading avatar";
            }
        }
        catch ( Exception ex )
        {
            message = $"Error uploading avatar: {ex.Message}";
        }
    }

    private async Task UpdateProfile()
    {
        if ( profile is null )
            return;

        isSaving = true;
        message = string.Empty;
        try
        {
            var updateRequest = new UpdateProfileRequest(
                profile.FirstName ,
                profile.LastName ,
                string.IsNullOrWhiteSpace( displayName ) ? null : displayName ,
                profile.DateOfBirth
            );

            var response = await Http.PutAsJsonAsync( "api/profile" , updateRequest );

            if ( response.IsSuccessStatusCode )
            {
                profile = await response.Content.ReadFromJsonAsync<ProfileDto>();
                message = "Profile updated successfully";
            }
            else
            {
                message = "Error updating profile";
            }
        }
        catch ( Exception ex )
        {
            message = $"Error: {ex.Message}";
        }
        finally
        {
            isSaving = false;
        }
    }

    private async Task ChangePassword()
    {
        if ( string.IsNullOrEmpty( currentPassword ) || string.IsNullOrEmpty( newPassword ) )
        {
            message = "Please fill all password fields";
            return;
        }

        if ( newPassword != confirmPassword )
        {
            message = "New passwords don't match";
            return;
        }

        isChangingPassword = true;
        message = string.Empty;
        try
        {
            var changeRequest = new ChangePasswordRequest( currentPassword , newPassword );

            var response = await Http.PostAsJsonAsync( "api/profile/change-password" , changeRequest );

            if ( response.IsSuccessStatusCode )
            {
                message = "Password changed successfully";
                currentPassword = string.Empty;
                newPassword = string.Empty;
                confirmPassword = string.Empty;
            }
            else
            {
                message = "Error changing password - check current password";
            }
        }
        catch ( Exception ex )
        {
            message = $"Error: {ex.Message}";
        }
        finally
        {
            isChangingPassword = false;
        }
    }

    private async Task Logout()
    {
        await AuthService.LogoutAsync();
        Navigation.NavigateTo( "/login" );
    }
}

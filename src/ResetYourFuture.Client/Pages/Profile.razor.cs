using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using ResetYourFuture.Client.Consumers;
using ResetYourFuture.Client.Interfaces;
using ResetYourFuture.Shared.DTOs;
using ResetYourFuture.Shared.Resources;

namespace ResetYourFuture.Client.Pages;

public partial class Profile
{
    [Inject] private IProfileConsumer ProfileConsumer { get; set; } = default!;
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
            profile = await ProfileConsumer.GetProfileAsync();
            if ( profile is null )
            {
                Navigation.NavigateTo( "/login" );
                return;
            }
            displayName = profile.DisplayName ?? string.Empty;
            await LoadAvatarAsync();
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
            var avatar = await ProfileConsumer.GetAvatarAsync();
            avatarDataUrl = avatar.HasValue
                ? $"data:{avatar.Value.ContentType};base64,{Convert.ToBase64String( avatar.Value.Data )}"
                : null;
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
            message = ProfileRes.FileTooLarge;
            return;
        }

        try
        {
            var success = await ProfileConsumer.UploadAvatarAsync( file );
            if ( success )
            {
                profile = await ProfileConsumer.GetProfileAsync();
                await LoadAvatarAsync();
                message = ProfileRes.AvatarUploadedSuccess;
            }
            else
            {
                message = ProfileRes.ErrorUploadingAvatar;
            }
        }
        catch ( Exception ex )
        {
            message = string.Format( ProfileRes.ErrorUploadingAvatarFormat, ex.Message );
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

            var updated = await ProfileConsumer.UpdateProfileAsync( updateRequest );

            if ( updated is not null )
            {
                profile = updated;
                message = ProfileRes.ProfileUpdatedSuccess;
            }
            else
            {
                message = ProfileRes.ErrorUpdatingProfile;
            }
        }
        catch ( Exception ex )
        {
            message = string.Format( ProfileRes.ErrorFormat, ex.Message );
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
            message = ProfileRes.FillAllPasswordFields;
            return;
        }

        if ( newPassword != confirmPassword )
        {
            message = ProfileRes.PasswordsDoNotMatch;
            return;
        }

        isChangingPassword = true;
        message = string.Empty;
        try
        {
            var changeRequest = new ChangePasswordRequest( currentPassword , newPassword );

            var success = await ProfileConsumer.ChangePasswordAsync( changeRequest );

            if ( success )
            {
                message = ProfileRes.PasswordChangedSuccess;
                currentPassword = string.Empty;
                newPassword = string.Empty;
                confirmPassword = string.Empty;
            }
            else
            {
                message = ProfileRes.ErrorChangingPassword;
            }
        }
        catch ( Exception ex )
        {
            message = string.Format( ProfileRes.ErrorFormat, ex.Message );
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

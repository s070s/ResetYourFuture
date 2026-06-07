using System.ComponentModel.DataAnnotations;
using ResetYourFuture.Shared.DTOs;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

/// <summary>
/// Validates DataAnnotations on the class-based request DTOs via <see cref="Validator"/>.
///
/// NOTE: the record-style request DTOs (SaveCourseRequest, SaveBlogArticleRequest,
/// SaveTestimonialRequest, StartConversationRequest, SaveModuleRequest, SaveLessonRequest)
/// put their attributes on the positional *constructor parameters*, not the generated
/// properties. Raw <c>Validator.TryValidateObject</c> (property-based) does not see those;
/// ASP.NET MVC's ModelState does. Their validation is therefore asserted in the controller
/// integration tests (Phase 5), not here.
/// </summary>
public class RequestDtoValidationTests
{
    private static bool IsValid( object model )
    {
        var ctx = new ValidationContext( model );
        return Validator.TryValidateObject( model, ctx, new List<ValidationResult>(), validateAllProperties: true );
    }

    private static RegisterRequestDto ValidRegister() => new()
    {
        Email = "user@example.com",
        Password = "Password1",
        ConfirmPassword = "Password1",
        FirstName = "First",
        LastName = "Last",
        GdprConsent = true
    };

    [Fact]
    public void Register_Valid_Passes() => IsValid( ValidRegister() ).ShouldBeTrue();

    [Fact]
    public void Register_InvalidEmail_Fails()
    {
        var dto = ValidRegister();
        dto.Email = "not-an-email";
        IsValid( dto ).ShouldBeFalse();
    }

    [Fact]
    public void Register_ShortPassword_Fails()
    {
        var dto = ValidRegister();
        dto.Password = dto.ConfirmPassword = "Ab1";
        IsValid( dto ).ShouldBeFalse();
    }

    [Fact]
    public void Register_ConfirmPasswordMismatch_Fails()
    {
        var dto = ValidRegister();
        dto.ConfirmPassword = "Different1";
        IsValid( dto ).ShouldBeFalse();
    }

    [Fact]
    public void Register_GdprConsentFalse_Fails()
    {
        var dto = ValidRegister();
        dto.GdprConsent = false;
        IsValid( dto ).ShouldBeFalse();
    }

    [Fact]
    public void Register_FirstNameTooLong_Fails()
    {
        var dto = ValidRegister();
        dto.FirstName = new string( 'x', 101 );
        IsValid( dto ).ShouldBeFalse();
    }

    [Fact]
    public void Login_Valid_Passes() =>
        IsValid( new LoginRequestDto { Email = "u@x.com", Password = "secret" } ).ShouldBeTrue();

    [Fact]
    public void Login_MissingEmail_Fails() =>
        IsValid( new LoginRequestDto { Email = "", Password = "secret" } ).ShouldBeFalse();

    [Fact]
    public void Login_InvalidEmail_Fails() =>
        IsValid( new LoginRequestDto { Email = "nope", Password = "secret" } ).ShouldBeFalse();

    [Fact]
    public void ResetPassword_Valid_Passes() =>
        IsValid( new ResetPasswordRequestDto
        {
            Email = "u@x.com",
            Token = "tok",
            NewPassword = "Password1",
            ConfirmPassword = "Password1"
        } ).ShouldBeTrue();

    [Fact]
    public void ResetPassword_Mismatch_Fails() =>
        IsValid( new ResetPasswordRequestDto
        {
            Email = "u@x.com",
            Token = "tok",
            NewPassword = "Password1",
            ConfirmPassword = "Nope12345"
        } ).ShouldBeFalse();

    [Fact]
    public void ResetPassword_ShortPassword_Fails() =>
        IsValid( new ResetPasswordRequestDto
        {
            Email = "u@x.com",
            Token = "tok",
            NewPassword = "Ab1",
            ConfirmPassword = "Ab1"
        } ).ShouldBeFalse();
}

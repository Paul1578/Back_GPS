using fletflow.Application.Auth.Validators;
using fletflow.Application.DTOs.Auth;

namespace BackEnd_Gps.Tests;

public sealed class PasswordPolicyValidatorTests
{
    [Fact]
    public void Registration_RequiresTwelveCharacters()
    {
        var validator = new RegisterUserValidator();
        var dto = new RegisterUserDto
        {
            Username = "fleet-user",
            Email = "fleet@example.test",
            Password = "Abc12345678",
        };

        var result = validator.Validate(dto);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(dto.Password));
        dto.Password = "Abc123456789";
        Assert.DoesNotContain(validator.Validate(dto).Errors, error => error.PropertyName == nameof(dto.Password));
    }

    [Fact]
    public void ChangePassword_RequiresTwelveCharacters()
    {
        var validator = new ChangePasswordValidator();
        var dto = new ChangePasswordDto
        {
            CurrentPassword = "Current123",
            NewPassword = "Abc12345678",
            ConfirmNewPassword = "Abc12345678",
        };

        Assert.Contains(validator.Validate(dto).Errors, error => error.PropertyName == nameof(dto.NewPassword));
        dto.NewPassword = "Abc123456789";
        dto.ConfirmNewPassword = dto.NewPassword;
        Assert.DoesNotContain(validator.Validate(dto).Errors, error => error.PropertyName == nameof(dto.NewPassword));
    }

    [Fact]
    public void PasswordReset_RequiresTwelveCharacters()
    {
        var validator = new ResetPasswordValidator();
        var dto = new ResetPasswordDto
        {
            Token = "valid-token",
            NewPassword = "Abc12345678",
            ConfirmNewPassword = "Abc12345678",
        };

        Assert.Contains(validator.Validate(dto).Errors, error => error.PropertyName == nameof(dto.NewPassword));
        dto.NewPassword = "Abc123456789";
        dto.ConfirmNewPassword = dto.NewPassword;
        Assert.DoesNotContain(validator.Validate(dto).Errors, error => error.PropertyName == nameof(dto.NewPassword));
    }
}

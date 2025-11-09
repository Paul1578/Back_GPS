using FluentValidation;
using fletflow.Application.DTOs.Auth;

public class LoginUserValidator : AbstractValidator<LoginUserDto>
{
    public LoginUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

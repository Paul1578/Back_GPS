using FluentValidation;
using fletflow.Application.DTOs.Auth;

public class RegisterUserValidator : AbstractValidator<RegisterUserDto>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MinimumLength(3);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
        RuleFor(x => x.RoleName).NotEmpty();
    }
}

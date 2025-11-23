using FluentValidation;
using fletflow.Application.DTOs.Auth;

namespace fletflow.Application.Auth.Validators
{
    public class ResetPasswordValidator : AbstractValidator<ResetPasswordDto>
    {
        public ResetPasswordValidator()
        {
            RuleFor(x => x.Token)
                .NotEmpty().WithMessage("El token es obligatorio.");

            RuleFor(x => x.NewPassword)
                .NotEmpty().WithMessage("La nueva contrasena es obligatoria.")
                .MinimumLength(6).WithMessage("La nueva contrasena debe tener al menos 6 caracteres.");

            RuleFor(x => x.ConfirmNewPassword)
                .Equal(x => x.NewPassword).WithMessage("La confirmacion no coincide con la nueva contrasena.");
        }
    }
}

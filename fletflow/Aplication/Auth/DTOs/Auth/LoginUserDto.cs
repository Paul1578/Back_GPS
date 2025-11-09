using System.ComponentModel.DataAnnotations;

namespace fletflow.Application.DTOs.Auth
{
    public class LoginUserDto
    {
        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress(ErrorMessage = "Debe proporcionar un correo electrónico válido.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        public string Password { get; set; }
    }
}

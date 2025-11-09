using System.ComponentModel.DataAnnotations;

namespace fletflow.Application.DTOs.Auth
{
    public class RegisterUserDto
    {
        [Required(ErrorMessage = "El nombre de usuario es obligatorio.")]
        [MinLength(3, ErrorMessage = "El nombre de usuario debe tener al menos 3 caracteres.")]
        public string Username { get; set; }

        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress(ErrorMessage = "Debe proporcionar un correo electrónico válido.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        public string Password { get; set; }

        public string RoleName { get; set; } = "User";
    }
}

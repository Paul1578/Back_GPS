using System.ComponentModel.DataAnnotations;

namespace fletflow.Application.DTOs.Auth
{
    public class LoginUserDto
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "A valid email address is required.")]
        public required string Email { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        public required string Password { get; set; }
    }
}

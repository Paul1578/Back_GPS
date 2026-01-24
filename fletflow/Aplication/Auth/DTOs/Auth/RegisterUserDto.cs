using System.ComponentModel.DataAnnotations;

namespace fletflow.Application.DTOs.Auth
{
    public class RegisterUserDto
    {
        [Required(ErrorMessage = "Username is required.")]
        [MinLength(3, ErrorMessage = "Username must be at least 3 characters.")]
        public required string Username { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "A valid email address is required.")]
        public required string Email { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
        public required string Password { get; set; }

        public string RoleName { get; set; } = "User";
    }
}

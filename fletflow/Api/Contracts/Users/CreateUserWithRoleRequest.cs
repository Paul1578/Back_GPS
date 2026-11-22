namespace fletflow.Api.Contracts.Users
{
    public class CreateUserWithRoleRequest
    {
        public string Username { get; set; } = default!;   // usuario para login
        public string Email { get; set; } = default!;      // email
        public string Password { get; set; } = default!;   // contraseña
        public string RoleName { get; set; } = default!;   // "Admin", "Driver", "Logistics", etc.
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? DocumentNumber { get; set; }
        public string? PhoneNumber { get; set; }
    }
}

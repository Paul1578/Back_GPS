namespace fletflow.Application.DTOs.Auth
{
    public class CreateUserWithRoleRequest
    {
        public string Username { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string RoleName { get; set; } = "User";
    }
}

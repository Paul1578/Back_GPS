    namespace fletflow.Application.DTOs.Users
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = default!;
        public string Email { get; set; } = default!;
        public List<string> Roles { get; set; } = new();
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

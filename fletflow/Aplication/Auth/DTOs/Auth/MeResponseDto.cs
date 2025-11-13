namespace fletflow.Application.DTOs.Auth
{
    public class MeResponseDto
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = default!;
        public string Email { get; set; } = default!;
        public bool IsActive { get; set; }
        public List<string> Roles { get; set; } = new();
    }
}

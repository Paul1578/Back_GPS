namespace fletflow.Application.DTOs.Auth
{
    public class AuthResponseDto
    {
        public string Token { get; set; } = null!;
        public DateTime Expiration { get; set; }
        public string RefreshToken { get; set; } = default!;     
        public DateTime RefreshTokenExpiration { get; set; }
    }
}

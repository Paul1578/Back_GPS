namespace fletflow.Application.DTOs.Auth
{
    public class ForgotPasswordResponseDto
    {
        public string Message { get; set; } = "Si el email existe, se envió un token.";
        public string? DebugToken { get; set; } // Solo ambiente dev
        public DateTime? ExpiresAt { get; set; }
    }
}

namespace fletflow.Application.DTOs.Auth
{
    public class ForgotPasswordResponseDto
    {
        public string Message { get; set; } = "If the email exists, a token was sent.";
        public string? DebugToken { get; set; } // Solo ambiente dev
        public DateTime? ExpiresAt { get; set; }
    }
}

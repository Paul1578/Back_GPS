namespace fletflow.Application.DTOs.Auth
{
    public class ForgotPasswordResponseDto
    {
        public string Message { get; set; } = "If the account exists and email delivery is available, reset instructions will be sent.";
        public string? DebugToken { get; set; } // Solo ambiente dev
        public DateTime? ExpiresAt { get; set; }
    }
}

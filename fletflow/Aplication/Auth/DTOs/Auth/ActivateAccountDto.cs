namespace fletflow.Application.DTOs.Auth
{
    public class ActivateAccountDto
    {
        public string Token { get; set; } = default!;
        public string NewPassword { get; set; } = default!;
        public string ConfirmNewPassword { get; set; } = default!;
    }
}

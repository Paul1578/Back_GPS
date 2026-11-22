namespace fletflow.Infrastructure.Services;

internal sealed record PasswordResetEmailPayload(string Email, string ResetLink, string PlainToken);

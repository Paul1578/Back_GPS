using fletflow.Application.DTOs.Auth;

namespace fletflow.Infrastructure.Services;

public interface IUserRegistrationService
{
    Task<AuthResponseDto> RegisterAsync(string username, string email, string password, string roleName = "User");
}

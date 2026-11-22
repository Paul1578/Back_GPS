using fletflow.Application.DTOs.Auth;

namespace fletflow.Infrastructure.Services;

public interface IUserRegistrationService
{
    Task RegisterAsync(string username, string email, string password, string roleName = "User");
}

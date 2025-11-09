namespace fletflow.Infrastructure.Auth.Contracts
{
    public interface IAuthService
    {
        Task<string> LoginAsync(string email, string password);
        Task<string> RegisterAsync(string email, string password, string fullName, string roleName);
        //Task<bool> ChangeUserStatusAsync(Guid userId, bool isActive);
    }
}

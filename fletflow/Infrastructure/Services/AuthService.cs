
using fletflow.Application.Auth.Commands;
using fletflow.Infrastructure.Auth.Contracts;
using fletflow.Infrastructure.Persistence.Contracts;
using fletflow.Infrastructure.Security;

namespace fletflow.Infrastructure.Services
{
    public class AuthService: IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly JwtTokenService _jwtService;

        public AuthService(IUnitOfWork unitOfWork, JwtTokenService jwtService)
        {
            _unitOfWork = unitOfWork;
            _jwtService = jwtService;
        }

        public async Task<string> RegisterAsync(string username, string email, string password, string roleName = "User")
        {

            var command = new RegisterUserCommand(_unitOfWork);

            var user = await command.Execute(username, email, password, roleName);

            var role = user.UserRoles.First().Role.Name;

            return _jwtService.GenerateToken(user.Id, user.Username, user.Email, role);
        }

        public async Task<string> LoginAsync(string email, string password)
        {

            var command = new LoginUserCommand(_unitOfWork);

            var user = await command.Execute(email, password);


            var role = user.UserRoles.First().Role.Name;

            return _jwtService.GenerateToken(user.Id, user.Username, user.Email, role);
        }
    }
}

using fletflow.Domain.Auth.Entities;
using fletflow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace fletflow.Aplication.Auth.Commands
{
    public class RegisterUserCommand
    {
        private readonly AppDbContext _context;
        public RegisterUserCommand(AppDbContext context)
        {
            _context = context;
        }
        public async Task<User> Execute(string username, string email, string password)
        {
            if (await _context.Users.AnyAsync(u => u.Email == email))
                throw new Exception("EL CORREO YA FUE REGISTRADO.");

            var user = new User
            {
                Username = username,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return user; 
        }
    }
}
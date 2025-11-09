
using Microsoft.EntityFrameworkCore;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Repositories;
using fletflow.Domain.Auth.Repositories;
using fletflow.Infrastructure.Security.Hashing;
using fletflow.Infrastructure.Security;
using fletflow.Infrastructure.Persistence;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Infrastructure.Config
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // DbContext
            services.AddDbContext<AppDbContext>(options =>
            {
                var conn = configuration.GetConnectionString("DefaultConnection");
                options.UseMySql(conn, ServerVersion.AutoDetect(conn));
            });

            // Repositories & UnitOfWork
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Security
            services.AddScoped<IPasswordHasher, PasswordHasher>();
            services.AddScoped<JwtTokenService>(); 

            return services;
        }
    }
}

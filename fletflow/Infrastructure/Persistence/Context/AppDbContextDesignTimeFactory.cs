using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace fletflow.Infrastructure.Persistence.Context;

public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        Env.Load();
        var host = Required("DB_HOST");
        var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "3306";
        var database = Required("DB_NAME");
        var user = Required("DB_USER");
        var password = Required("DB_PASSWORD");
        var connectionString = $"Server={host};Port={port};Database={database};User={user};Password={password};TreatTinyAsBoolean=false;";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;

        return new AppDbContext(options);
    }

    private static string Required(string key) =>
        Environment.GetEnvironmentVariable(key) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Falta la variable de entorno {key} para operaciones de EF Core.");
}

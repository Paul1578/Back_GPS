using System.Reflection;

namespace fletflow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(c => c.Where(t =>
                t.Name.EndsWith("Command") || t.Name.EndsWith("Query")))
            .AsSelf()
            .WithScopedLifetime());

        return services;
    }
}

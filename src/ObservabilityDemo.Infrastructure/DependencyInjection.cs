using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ObservabilityDemo.Application.Abstractions.Persistence;
using ObservabilityDemo.Infrastructure.Persistence;
using ObservabilityDemo.Infrastructure.Persistence.WorkItems;

namespace ObservabilityDemo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:Postgres must be configured.");
        }

        services.AddSingleton(new PostgresConnectionString(connectionString));
        services.AddScoped<IWorkItemRepository, DapperWorkItemRepository>();

        return services;
    }
}

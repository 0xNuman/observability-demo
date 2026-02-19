using Microsoft.Extensions.DependencyInjection;
using ObservabilityDemo.Application.WorkItems;

namespace ObservabilityDemo.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IWorkItemService, WorkItemService>();

        return services;
    }
}

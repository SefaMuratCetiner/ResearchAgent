using Microsoft.Extensions.DependencyInjection;
using Research.Planning.Application.CreatePlan;

namespace Research.Planning.Application;

/// <summary>DI registrations for the Planning Application layer.</summary>
public static class PlanningApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddPlanningApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ResearchPlanningService>();

        return services;
    }
}

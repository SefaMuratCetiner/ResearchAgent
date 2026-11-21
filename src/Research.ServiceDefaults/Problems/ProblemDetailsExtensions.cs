using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Research.ServiceDefaults.Exceptions;

namespace Research.ServiceDefaults.Problems;

/// <summary>Registers ASP.NET Core's built-in ProblemDetails and <see cref="CustomExceptionHandler"/>.</summary>
public static class ProblemDetailsExtensions
{
    public static IServiceCollection AddResearchProblemDetails(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProblemDetails(static options =>
        {
            options.CustomizeProblemDetails = static context =>
            {
                context.ProblemDetails.Instance ??= context.HttpContext.Request.Path.Value;
                context.ProblemDetails.Extensions.TryAdd(
                    "traceId",
                    Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier);
            };
        });

        services.AddExceptionHandler<CustomExceptionHandler>();
        return services;
    }
}

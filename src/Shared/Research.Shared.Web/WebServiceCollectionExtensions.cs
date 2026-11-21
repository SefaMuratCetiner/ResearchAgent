using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Research.Shared.Web.Exceptions;

namespace Research.Shared.Web;

/// <summary>DI registrations for shared HTTP presentation concerns (ProblemDetails and global exception handling).</summary>
public static class WebServiceCollectionExtensions
{
    /// <summary>
    /// Registers ASP.NET Core's built-in ProblemDetails (enriching every problem with <c>instance</c> and <c>traceId</c>)
    /// and <see cref="CustomExceptionHandler"/> as an <see cref="Microsoft.AspNetCore.Diagnostics.IExceptionHandler"/>.
    /// </summary>
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

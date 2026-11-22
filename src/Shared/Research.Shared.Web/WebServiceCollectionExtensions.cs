using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Research.Shared.Web.Exceptions;

namespace Research.Shared.Web;

/// <summary>DI registrations for shared HTTP presentation concerns (ProblemDetails, global exception handling, and JSON defaults).</summary>
public static class WebServiceCollectionExtensions
{
    /// <summary>
    /// Registers ProblemDetails, <see cref="CustomExceptionHandler"/>, and the shared HTTP JSON serialization defaults
    /// (omit null properties, serialize enums as strings).
    /// </summary>
    public static IServiceCollection AddResearchHttpApiDefaults(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddResearchProblemDetails();
        services.ConfigureHttpJsonOptions(static options =>
        {
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        return services;
    }

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

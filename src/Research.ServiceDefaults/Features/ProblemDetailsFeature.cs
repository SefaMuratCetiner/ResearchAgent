using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Research.ServiceDefaults.Composition;
using Research.ServiceDefaults.Exceptions;

namespace Research.ServiceDefaults.Features;

/// <summary>
/// Registers ASP.NET Core's built-in ProblemDetails (enriching every problem with <c>instance</c> and <c>traceId</c>)
/// and <see cref="CustomExceptionHandler"/> as an <see cref="Microsoft.AspNetCore.Diagnostics.IExceptionHandler"/>.
/// </summary>
public sealed class ProblemDetailsFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.Services.AddProblemDetails(static options =>
        {
            options.CustomizeProblemDetails = static context =>
            {
                context.ProblemDetails.Instance ??= context.HttpContext.Request.Path.Value;
                context.ProblemDetails.Extensions.TryAdd(
                    "traceId",
                    Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier);
            };
        });

        app.Services.AddExceptionHandler<CustomExceptionHandler>();
    }
}

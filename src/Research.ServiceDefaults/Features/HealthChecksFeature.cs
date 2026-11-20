using Microsoft.Extensions.DependencyInjection;
using Research.ServiceDefaults.Composition;

namespace Research.ServiceDefaults.Features;

/// <summary>
/// The health check service. Aspire client integrations add their checks (Postgres, RabbitMQ); Host features add
/// the service's own (schema, Degraded checks for Valkey and blob storage). <see cref="HealthEndpoints"/> maps them.
/// </summary>
public sealed class HealthChecksFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.Services.AddHealthChecks();
    }
}

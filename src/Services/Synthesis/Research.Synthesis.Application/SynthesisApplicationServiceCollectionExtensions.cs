using Microsoft.Extensions.DependencyInjection;
using Research.Shared.Bus.Mediation;
using Research.Shared.Kernel.Results;
using Research.Synthesis.Application.WriteReport;

namespace Research.Synthesis.Application;

/// <summary>DI registrations for the Synthesis Application layer.</summary>
public static class SynthesisApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddSynthesisApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IRequestHandler<WriteReportCommand, Result<WrittenReport>>, WriteReportHandler>();

        return services;
    }
}

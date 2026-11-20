using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Research.ServiceDefaults.Composition;

/// <summary>What an <see cref="IResearchFeature"/> registers into, plus the helpers features share.</summary>
public sealed class ResearchBuilder(IHostApplicationBuilder builder)
{
    public IServiceCollection Services => builder.Services;

    public IConfigurationManager Configuration => builder.Configuration;

    public IHostEnvironment Environment => builder.Environment;

    public ILoggingBuilder Logging => builder.Logging;

    /// <summary>The underlying builder, for Aspire client integrations that extend it (<c>AddNpgsqlDataSource</c>).</summary>
    public IHostApplicationBuilder Host => builder;

    /// <summary>Decorates the existing registration of <typeparamref name="TService"/> in place, keeping its lifetime.</summary>
    public void Decorate<TService>(Func<IServiceProvider, TService, TService> decorate)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(decorate);
        var descriptor = Services.LastOrDefault(static d => d.ServiceType == typeof(TService) && !d.IsKeyedService)
            ?? throw new InvalidOperationException($"No registration of {typeof(TService).Name} to decorate.");

        Services[Services.IndexOf(descriptor)] = ServiceDescriptor.Describe(
            typeof(TService),
            provider => decorate(provider, (TService)CreateInner(provider, descriptor)),
            descriptor.Lifetime);
    }

    private static object CreateInner(IServiceProvider provider, ServiceDescriptor descriptor) =>
        descriptor.ImplementationInstance
        ?? descriptor.ImplementationFactory?.Invoke(provider)
        ?? ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);
}

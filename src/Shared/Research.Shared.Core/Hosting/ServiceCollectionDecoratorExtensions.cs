using Microsoft.Extensions.DependencyInjection;

namespace Research.Shared.Core.Hosting;

/// <summary>Service-collection decoration helpers shared by composition roots and infrastructure layers.</summary>
public static class ServiceCollectionDecoratorExtensions
{
    /// <summary>Decorates the existing registration of <typeparamref name="TService"/> in place, keeping its lifetime.</summary>
    public static IServiceCollection Decorate<TService>(
        this IServiceCollection services,
        Func<IServiceProvider, TService, TService> decorate)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(decorate);

        var descriptor = services.LastOrDefault(static d => d.ServiceType == typeof(TService) && !d.IsKeyedService)
            ?? throw new InvalidOperationException($"No registration of {typeof(TService).Name} to decorate.");

        services[services.IndexOf(descriptor)] = ServiceDescriptor.Describe(
            typeof(TService),
            provider => decorate(provider, (TService)CreateInner(provider, descriptor)),
            descriptor.Lifetime);

        return services;
    }

    private static object CreateInner(IServiceProvider provider, ServiceDescriptor descriptor) =>
        descriptor.ImplementationInstance
        ?? descriptor.ImplementationFactory?.Invoke(provider)
        ?? ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);
}

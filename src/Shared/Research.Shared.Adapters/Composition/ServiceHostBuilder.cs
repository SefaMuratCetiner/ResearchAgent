using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Research.Shared.Adapters.Composition;

/// <summary>What an <see cref="IHostFeature"/> registers into, plus the helpers features share.</summary>
public sealed class ServiceHostBuilder(IHostApplicationBuilder builder)
{
    public IServiceCollection Services => builder.Services;

    public IConfigurationManager Configuration => builder.Configuration;

    public IHostEnvironment Environment => builder.Environment;

    public ILoggingBuilder Logging => builder.Logging;

    /// <summary>The underlying builder, for Aspire client integrations that extend it (<c>AddNpgsqlDataSource</c>).</summary>
    public IHostApplicationBuilder Host => builder;

    /// <summary>Decorates the existing registration of <typeparamref name="TService"/> in place, keeping its lifetime.</summary>
    public void Decorate<TService>(Func<IServiceProvider, TService, TService> decorate)
        where TService : class =>
        Services.Decorate(decorate);
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Research.Providers.Postgres.Dapper;
using Research.Providers.Postgres.Health;
using Research.Providers.Postgres.Messaging;
using Research.Providers.Postgres.Transactions;
using Research.Shared.Bus.Transactions;
using Research.Shared.Bus.Messaging;

namespace Research.Providers.Postgres;

/// <summary>DI registrations for the Postgres transactions, transactional outbox/inbox, and schema readiness check.</summary>
public static class PostgresServiceCollectionExtensions
{
    /// <summary>
    /// Registers Dapper type handlers, <see cref="PostgresUnitOfWork"/> (<see cref="IUnitOfWork"/>),
    /// <see cref="PostgresOutbox"/> (<see cref="IOutbox"/>), <see cref="PostgresInbox"/> (<see cref="IInbox"/>),
    /// <see cref="PostgresOutboxStore"/> (<see cref="IOutboxStore"/>), and the readiness check verifying
    /// <paramref name="requiredTables"/> exist. The <c>NpgsqlDataSource</c> is supplied by the Host's Aspire integration.
    /// </summary>
    public static IServiceCollection AddPostgresProvider(
        this IServiceCollection services,
        string schemaHealthCheckName,
        IReadOnlyList<string> requiredTables)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaHealthCheckName);
        ArgumentNullException.ThrowIfNull(requiredTables);

        DapperTypeHandlers.Register();

        services.TryAddScoped<PostgresUnitOfWork>();
        services.TryAddScoped<IUnitOfWork>(static provider => provider.GetRequiredService<PostgresUnitOfWork>());
        services.TryAddScoped<IOutbox, PostgresOutbox>();
        services.TryAddScoped<IInbox, PostgresInbox>();
        services.TryAddSingleton<IOutboxStore, PostgresOutboxStore>();

        services.AddHealthChecks().Add(new HealthCheckRegistration(
            schemaHealthCheckName,
            provider => new PostgresSchemaHealthCheck(provider.GetRequiredService<NpgsqlDataSource>(), requiredTables),
            failureStatus: null,
            tags: ["ready"]));

        return services;
    }
}

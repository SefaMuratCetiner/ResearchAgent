using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Research.Shared.Core.Embeddings;

namespace Research.Providers.MockEmbeddings;

/// <summary>DI registrations for the offline hashing mock embedding generator.</summary>
public static class MockEmbeddingsServiceCollectionExtensions
{
    /// <summary>Registers <see cref="HashingEmbeddingGenerator"/> as <see cref="IEmbeddingGenerator"/>.</summary>
    public static IServiceCollection AddMockEmbeddings(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IEmbeddingGenerator, HashingEmbeddingGenerator>();
        return services;
    }
}

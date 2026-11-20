using Research.ServiceDefaults.Composition;
using Research.Providers.MockEmbeddings;
using Research.Shared.Core.Embeddings;

namespace Research.Knowledge.Bootstrap.Features;

/// <summary>
/// The embedding model: the offline hashing mock for now. A real model replaces this one registration (and needs a
/// schema script for its vector size; documents are keyed by model, so old and new vectors never mix).
/// </summary>
internal sealed class EmbeddingsFeature : IResearchFeature
{
    public void Register(ResearchBuilder app) =>
        app.Services.AddSingleton<IEmbeddingGenerator, HashingEmbeddingGenerator>();
}

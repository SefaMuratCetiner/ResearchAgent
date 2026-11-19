using Microsoft.Extensions.Diagnostics.HealthChecks;
using Research.Hosting.Composition;
using Research.Providers.BlobStorage;
using Research.SharedKernel.Documents;

namespace Research.Fetching.Host.Features;

/// <summary>
/// The document store on Azure Blob Storage (Aspire connection <c>blobs</c>, Azurite locally). Aspire's own check is
/// off: ours reports Degraded.
/// </summary>
internal sealed class BlobStorageFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        app.Host.AddAzureBlobServiceClient("blobs", static settings => settings.DisableHealthChecks = true);
        app.Services.AddSingleton<IDocumentStore, BlobDocumentStore>();
        app.Services.AddHealthChecks().AddCheck<BlobStorageHealthCheck>("blobs", failureStatus: HealthStatus.Degraded);
    }
}

using Research.Providers.BlobStorage;
using Research.Shared.Adapters.Composition;

namespace Research.Knowledge.Bootstrap.Features;

/// <summary>
/// The document store on Azure Blob Storage (Aspire connection <c>blobs</c>, Azurite locally), read-only here.
/// Aspire's own check is off: ours reports Degraded.
/// </summary>
internal sealed class BlobStorageFeature : IHostFeature
{
    public void Register(ServiceHostBuilder app)
    {
        app.Host.AddAzureBlobServiceClient("blobs", static settings => settings.DisableHealthChecks = true);
        app.Services.AddBlobDocumentStore();
    }
}

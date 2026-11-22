using Azure;
using Azure.Storage.Blobs;
using Research.Shared.Core.Documents;

namespace Research.Providers.BlobStorage;

/// <summary>
/// Page text as UTF-8 blobs in the <c>documents</c> container, created on first use. Writes overwrite, so a retried
/// fetch is harmless.
/// </summary>
public sealed class BlobDocumentStore(BlobServiceClient serviceClient) : IDocumentStore, IDisposable
{
    public const string ContainerName = "documents";

    private readonly SemaphoreSlim _containerGate = new(1, 1);
    private volatile bool _containerReady;

    public void Dispose() => _containerGate.Dispose();

    public async Task SaveTextAsync(string key, string text, CancellationToken cancellationToken)
    {
        var container = await GetContainerAsync(cancellationToken).ConfigureAwait(false);
        await container.GetBlobClient(key)
            .UploadAsync(BinaryData.FromString(text), overwrite: true, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<string?> ReadTextAsync(string key, CancellationToken cancellationToken)
    {
        var container = serviceClient.GetBlobContainerClient(ContainerName);
        try
        {
            var result = await container.GetBlobClient(key).DownloadContentAsync(cancellationToken).ConfigureAwait(false);
            return result.Value.Content.ToString();
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    private async Task<BlobContainerClient> GetContainerAsync(CancellationToken cancellationToken)
    {
        var container = serviceClient.GetBlobContainerClient(ContainerName);
        if (_containerReady)
        {
            return container;
        }

        await _containerGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_containerReady)
            {
                await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                _containerReady = true;
            }
        }
        finally
        {
            _containerGate.Release();
        }

        return container;
    }
}

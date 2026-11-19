namespace Research.SharedKernel.Documents;

/// <summary>
/// Large text (page content) kept out of messages and rows: the claim-check store. Messages carry the key only.
/// Saving the same key twice overwrites, so a retried fetch is harmless.
/// </summary>
public interface IDocumentStore
{
    Task SaveTextAsync(string key, string text, CancellationToken cancellationToken);

    /// <returns>The text, or null when no document has that key.</returns>
    Task<string?> ReadTextAsync(string key, CancellationToken cancellationToken);
}

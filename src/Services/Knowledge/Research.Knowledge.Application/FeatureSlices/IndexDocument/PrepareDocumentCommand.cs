using Research.Shared.Bus.Mediation;
using Research.Shared.Kernel.Results;

namespace Research.Knowledge.Application.IndexDocument;

/// <summary>Index the text stored under <see cref="BlobKey"/> (fetched from <see cref="Url"/>).</summary>
public sealed record PrepareDocumentCommand(string Url, string Title, string BlobKey, string ContentHash)
    : ICommand<Result<PreparedDocument>>;

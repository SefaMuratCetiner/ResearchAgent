using Research.Fetching.Domain;
using Research.Shared.Core.Transactions;

namespace Research.Fetching.Application.Common.Interfaces;

/// <summary>Persistence of <see cref="FetchedDocument"/>.</summary>
public interface IFetchedDocumentRepository
{
    /// <summary>Inserts the row, or refreshes the existing row for the same job and URL.</summary>
    /// <returns>The id of the stored row (the existing id when the row was already there).</returns>
    Task<Guid> UpsertAsync(FetchedDocument document, ITransactionScope transaction, CancellationToken cancellationToken);
}

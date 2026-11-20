namespace Research.Shared.Core.Transactions;

/// <summary>Opens a connection and begins a transaction on the service's own database.</summary>
public interface ITransactionFactory
{
    Task<ITransactionScope> BeginAsync(CancellationToken cancellationToken);
}

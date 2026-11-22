namespace Research.Shared.Bus.Transactions;

/// <summary>
/// Opt-in marker for commands whose entire execution should be wrapped in a single <see cref="Transactions.IUnitOfWork"/>
/// transaction by <c>TransactionBehavior</c>, committing on success and rolling back on failure or exception.
/// </summary>
public interface ITransactionalCommand;

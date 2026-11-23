using Research.Shared.Bus.Mediation;

namespace Research.Jobs.Application.RecoverStuckJobs;

/// <summary>Re-emits or fails stuck steps through <see cref="StuckJobRecoveryService"/>.</summary>
public sealed class RecoverStuckJobsHandler(StuckJobRecoveryService recovery) : ICommandHandler<RecoverStuckJobsCommand, int>
{
    public Task<int> HandleAsync(RecoverStuckJobsCommand request, CancellationToken cancellationToken) =>
        recovery.RecoverAsync(cancellationToken);
}

using Microsoft.Extensions.Options;
using Research.Jobs.RecoverStuckJobs;

namespace Research.Jobs.Host.Workers;

/// <summary>Runs <see cref="RecoverStuckJobsHandler"/> every <see cref="JobsOptions.RecoveryInterval"/>.</summary>
internal sealed class StuckJobRecoveryWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<JobsOptions> options,
    TimeProvider timeProvider,
    ILogger<StuckJobRecoveryWorker> logger) : PeriodicWorker(scopeFactory, timeProvider, logger)
{
    protected override string Name => "stuck-job-recovery";

    protected override TimeSpan Interval => options.CurrentValue.RecoveryInterval;

    protected override Task<int> RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<RecoverStuckJobsHandler>().HandleAsync(cancellationToken);
}

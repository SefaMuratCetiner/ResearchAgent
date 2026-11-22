using Microsoft.Extensions.Options;
using Research.Jobs.Application;
using Research.Jobs.Application.RecoverStuckJobs;
using Research.Shared.Bus.Mediation;

namespace Research.Jobs.Worker.Workers;

/// <summary>Dispatches <see cref="RecoverStuckJobsCommand"/> every <see cref="JobsOptions.RecoveryInterval"/>.</summary>
internal sealed class StuckJobRecoveryWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<JobsOptions> options,
    TimeProvider timeProvider,
    ILogger<StuckJobRecoveryWorker> logger) : PeriodicWorker(scopeFactory, timeProvider, logger)
{
    protected override string Name => "stuck-job-recovery";

    protected override TimeSpan Interval => options.CurrentValue.RecoveryInterval;

    protected override Task<int> RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<ISender>().SendAsync(new RecoverStuckJobsCommand(), cancellationToken);
}

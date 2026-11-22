using Microsoft.Extensions.Options;
using Research.Jobs.Application;
using Research.Jobs.Application.EnforceJobDeadlines;
using Research.Shared.Bus.Mediation;

namespace Research.Jobs.Worker.Workers;

/// <summary>Dispatches <see cref="EnforceJobDeadlinesCommand"/> every <see cref="JobsOptions.DeadlineInterval"/>.</summary>
internal sealed class JobDeadlineWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<JobsOptions> options,
    TimeProvider timeProvider,
    ILogger<JobDeadlineWorker> logger) : PeriodicWorker(scopeFactory, timeProvider, logger)
{
    protected override string Name => "job-deadlines";

    protected override TimeSpan Interval => options.CurrentValue.DeadlineInterval;

    protected override Task<int> RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<ISender>().SendAsync(new EnforceJobDeadlinesCommand(), cancellationToken);
}

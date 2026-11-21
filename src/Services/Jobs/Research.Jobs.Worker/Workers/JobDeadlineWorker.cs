using Microsoft.Extensions.Options;
using Research.Jobs.Application;
using Research.Jobs.Application.EnforceJobDeadlines;

namespace Research.Jobs.Worker.Workers;

/// <summary>Runs <see cref="EnforceJobDeadlinesHandler"/> every <see cref="JobsOptions.DeadlineInterval"/>.</summary>
internal sealed class JobDeadlineWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<JobsOptions> options,
    TimeProvider timeProvider,
    ILogger<JobDeadlineWorker> logger) : PeriodicWorker(scopeFactory, timeProvider, logger)
{
    protected override string Name => "job-deadlines";

    protected override TimeSpan Interval => options.CurrentValue.DeadlineInterval;

    protected override Task<int> RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<EnforceJobDeadlinesHandler>().HandleAsync(cancellationToken);
}

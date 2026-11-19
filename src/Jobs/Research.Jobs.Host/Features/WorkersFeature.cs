using Research.Hosting.Composition;
using Research.Jobs.Host.Workers;

namespace Research.Jobs.Host.Features;

/// <summary>The timer workers. Both run on every replica; row locks with SKIP LOCKED share the work.</summary>
internal sealed class WorkersFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        app.Services.AddHostedService<StuckJobRecoveryWorker>();
        app.Services.AddHostedService<JobDeadlineWorker>();
    }
}

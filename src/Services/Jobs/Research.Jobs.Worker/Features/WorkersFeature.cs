using Research.Shared.Adapters.Composition;
using Research.Jobs.Worker.Workers;

namespace Research.Jobs.Worker.Features;

/// <summary>The timer workers. Both run on every replica; row locks with SKIP LOCKED share the work.</summary>
internal sealed class WorkersFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        app.Services.AddHostedService<StuckJobRecoveryWorker>();
        app.Services.AddHostedService<JobDeadlineWorker>();
    }
}

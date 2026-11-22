using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Domain;
using Research.Shared.Core.Mediation;
using Research.SharedKernel.Results;

namespace Research.Jobs.Application.GetResearchJob;

/// <summary>Reads a job and summarizes its steps by kind.</summary>
public sealed class GetResearchJobHandler(IResearchJobRepository jobs, IJobStepRepository steps)
    : IQueryHandler<GetResearchJobQuery, Result<ResearchJobView>>
{
    public async Task<Result<ResearchJobView>> HandleAsync(GetResearchJobQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var job = await jobs.GetAsync(query.JobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return Result<ResearchJobView>.Failure(JobsErrors.JobNotFound);
        }

        var jobSteps = await steps.ListAsync(job.Id, cancellationToken).ConfigureAwait(false);
        var progress = jobSteps
            .GroupBy(static step => step.Kind)
            .OrderBy(static group => group.Key)
            .Select(static group => new StepProgress(
                group.Key,
                group.Count(static s => s.Status == StepStatus.Running),
                group.Count(static s => s.Status == StepStatus.Succeeded),
                group.Count(static s => s.Status == StepStatus.Failed)))
            .ToList();

        return Result<ResearchJobView>.Success(new ResearchJobView(job, progress));
    }
}

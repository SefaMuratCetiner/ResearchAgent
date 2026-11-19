using Research.Jobs.Domain;
using Research.Jobs.Domain.Ports;
using Research.SharedKernel.Results;

namespace Research.Jobs.GetResearchReport;

/// <summary>
/// The report of a completed job. Active jobs answer <c>jobs.report_not_ready</c>; jobs that ended without one
/// (failed, cancelled, expired) answer <c>jobs.report_not_available</c>.
/// </summary>
public sealed class GetResearchReportHandler(IResearchJobRepository jobs, IResearchReportRepository reports)
{
    public async Task<Result<ResearchReportView>> HandleAsync(GetResearchReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var job = await jobs.GetAsync(query.JobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return Result<ResearchReportView>.Failure(JobsErrors.JobNotFound);
        }

        if (!job.Status.IsTerminal())
        {
            return Result<ResearchReportView>.Failure(JobsErrors.ReportNotReady);
        }

        var report = job.Status.HasReport()
            ? await reports.GetAsync(job.Id, cancellationToken).ConfigureAwait(false)
            : null;

        return report is null
            ? Result<ResearchReportView>.Failure(JobsErrors.ReportNotAvailable)
            : Result<ResearchReportView>.Success(new ResearchReportView(job, report));
    }
}

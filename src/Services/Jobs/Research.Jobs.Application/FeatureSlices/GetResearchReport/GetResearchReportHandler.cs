using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Application.Common.Models;
using Research.Jobs.Domain;
using Research.Shared.Core.Mediation;
using Research.Shared.Kernel.Results;

namespace Research.Jobs.Application.GetResearchReport;

/// <summary>
/// The report of a completed job. Active jobs answer <c>jobs.report_not_ready</c>; jobs that ended without one
/// (failed, cancelled, expired) answer <c>jobs.report_not_available</c>.
/// </summary>
public sealed class GetResearchReportHandler(IResearchJobRepository jobs, IResearchReportRepository reports)
    : IQueryHandler<GetResearchReportQuery, Result<ResearchReportDto>>
{
    public async Task<Result<ResearchReportDto>> HandleAsync(GetResearchReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var job = await jobs.GetAsync(query.JobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return Result<ResearchReportDto>.Failure(JobsErrors.JobNotFound);
        }

        if (!job.Status.IsTerminal())
        {
            return Result<ResearchReportDto>.Failure(JobsErrors.ReportNotReady);
        }

        var report = job.Status.HasReport()
            ? await reports.GetAsync(job.Id, cancellationToken).ConfigureAwait(false)
            : null;

        return report is null
            ? Result<ResearchReportDto>.Failure(JobsErrors.ReportNotAvailable)
            : Result<ResearchReportDto>.Success(ResearchReportDto.From(job, report));
    }
}

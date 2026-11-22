using Research.Shared.Core.Mediation;
using Research.SharedKernel.Results;

namespace Research.Jobs.Application.GetResearchJob;

public sealed record GetResearchJobQuery(Guid JobId) : IQuery<Result<ResearchJobView>>, IJobScopedRequest;

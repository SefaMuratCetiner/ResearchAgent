using Research.Shared.Bus.Mediation;
using Research.Shared.Kernel.Results;

namespace Research.Jobs.Application.GetResearchJob;

public sealed record GetResearchJobQuery(Guid JobId) : IQuery<Result<ResearchJobView>>, IJobScopedRequest;

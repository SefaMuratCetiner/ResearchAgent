using Research.Fetching.Application;
using Research.Jobs.Application;
using Research.Knowledge.Application;
using Research.Planning.Application;
using Research.Search.Application;
using Research.Shared.Core.Errors;
using Research.Shared.Core.Messaging;
using Research.Shared.Web.Problems;
using Research.SharedKernel.Results;
using Research.Synthesis.Application;

namespace Research.Infrastructure.UnitTests.ServiceDefaults;

public sealed class ErrorCatalogTests
{
    /// <summary>Every contributor in the solution, as the Hosts register them.</summary>
    private static readonly IErrorCatalogContributor[] All =
    [
        new ProblemErrorCatalog(),
        new MessagingErrorCatalog(),
        new JobsErrorCatalog(),
        new PlanningErrorCatalog(),
        new SearchErrorCatalog(),
        new FetchingErrorCatalog(),
        new KnowledgeErrorCatalog(),
        new SynthesisErrorCatalog(),
    ];

    [Fact]
    public void AllContributors_FormAValidCatalog()
    {
        var catalog = new ErrorCatalog(All);
        catalog.All.ShouldNotBeEmpty();
        catalog.All.ShouldAllBe(static e => System.Text.RegularExpressions.Regex.IsMatch(e.Code, "^[a-z]+\\.[a-z_]+$"));
    }

    [Fact]
    public void DuplicateCode_IsRejected() =>
        Should.Throw<InvalidOperationException>(() => new ErrorCatalog([new JobsErrorCatalog(), new JobsErrorCatalog()]));

    [Fact]
    public void ForeignPrefix_IsRejected() =>
        Should.Throw<InvalidOperationException>(() => new ErrorCatalog([new Foreign()]))
            .Message.ShouldContain("does not start with");

    private sealed class Foreign : IErrorCatalogContributor
    {
        public string Prefix => "mine.";

        public IEnumerable<Error> GetErrors() => [new Error("other.code", ErrorKind.Rule, "x")];
    }
}

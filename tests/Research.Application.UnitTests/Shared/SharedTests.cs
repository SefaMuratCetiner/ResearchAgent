using Research.Shared.Core.Messaging;
using Research.Fetching;
using Research.ServiceDefaults.Errors;
using Research.Jobs;
using Research.Knowledge;
using Research.Shared.Adapters.Messaging;
using Research.Planning;
using Research.ServiceDefaults.Middleware;
using Research.ServiceDefaults.Problems;
using Research.Search;
using Research.Shared.Core.Errors;
using Research.Shared.Core.Logging;
using Research.SharedKernel.Results;
using Research.Synthesis;

namespace Research.Application.UnitTests.Shared;

public sealed class ContentTextTests
{
    [Fact]
    public void ShortValue_IsUnchanged() => ContentText.Truncate("abc", 10).ShouldBe("abc");

    [Fact]
    public void Null_IsEmpty() => ContentText.Truncate(null, 10).ShouldBe(string.Empty);

    [Fact]
    public void LongValue_IsCutWithMarker() => ContentText.Truncate("abcdefghij", 4).ShouldBe("abcd…[truncated 6 chars]");

    [Fact]
    public void NonPositiveLimit_MeansNoLimit() => ContentText.Truncate("abcdef", 0).ShouldBe("abcdef");
}

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

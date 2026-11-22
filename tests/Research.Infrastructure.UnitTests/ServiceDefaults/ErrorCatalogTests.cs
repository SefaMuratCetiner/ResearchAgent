using System.Text.RegularExpressions;
using Research.Fetching.Domain;
using Research.Jobs.Domain;
using Research.Knowledge.Application;
using Research.Planning.Application;
using Research.Shared.Core.Errors;
using Research.Shared.Messaging;
using Research.Shared.Ports.Search;
using Research.Shared.Web.Problems;
using Research.Shared.Kernel.Results;
using Research.Synthesis.Application;

namespace Research.Infrastructure.UnitTests.ServiceDefaults;

public sealed class ErrorCatalogTests
{
    /// <summary>Every error set in the solution, as the Hosts register them.</summary>
    private static readonly ErrorSet[] All =
    [
        ApiErrors.All,
        MessagingErrors.All,
        JobsErrors.All,
        PlanningErrors.All,
        SearchErrors.All,
        FetchingErrors.All,
        KnowledgeErrors.All,
        SynthesisErrors.All,
    ];

    [Fact]
    public void AllErrorSets_FormAValidCatalog()
    {
        var catalog = new ErrorCatalog(All);
        catalog.All.ShouldNotBeEmpty();
        catalog.All.ShouldAllBe(static e => Regex.IsMatch(e.Code, "^[a-z]+\\.[a-z_]+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void DuplicateCode_IsRejected() =>
        Should.Throw<InvalidOperationException>(() => new ErrorCatalog([JobsErrors.All, JobsErrors.All]));

    [Fact]
    public void ForeignPrefix_IsRejected() =>
        Should.Throw<InvalidOperationException>(() => new ErrorCatalog([new ErrorSet("mine.", [new Error("other.code", ErrorKind.Rule, "x")])]))
            .Message.ShouldContain("does not start with");
}

using Research.Jobs;
using Research.Jobs.AdvanceOnPlanReady;

namespace Research.Application.UnitTests.Jobs;

public sealed class PlanNormalizationTests
{
    [Fact]
    public void Normalize_TrimsDropsBlanksDeduplicatesAndCaps()
    {
        var result = PlanReadyMessageHandler.Normalize(
            ["  What is A? ", "", "what is a?", "   ", "What is B?", "What is C?"],
            max: 2);

        result.ShouldBe(["What is A?", "What is B?"]);
    }

    [Fact]
    public void Normalize_NullPlan_IsEmpty() => PlanReadyMessageHandler.Normalize(null, 5).ShouldBeEmpty();

    [Fact]
    public void Normalize_MakesOneCleanLineAndCutsLongOnes()
    {
        var result = PlanReadyMessageHandler.Normalize(
            ["What\u200B is\nA?\u202E", new string('x', 500)],
            max: 5,
            maxLength: 50);

        result.ShouldBe(["What is A?", new string('x', 50)]);
    }
}

public sealed class JobsOptionsValidatorTests
{
    [Fact]
    public void Defaults_AreValid()
    {
        new JobsOptionsValidator().Validate(null, new JobsOptions()).Succeeded.ShouldBeTrue();
        new JobsOptionsConsistencyValidator().Validate(null, new JobsOptions()).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void ReserveLongerThanDuration_Fails()
    {
        var options = new JobsOptions { MaxDuration = TimeSpan.FromMinutes(1), SynthesisReserve = TimeSpan.FromMinutes(2) };
        new JobsOptionsConsistencyValidator().Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void MinSourcesAboveMaxSources_Fails()
    {
        var options = new JobsOptions { MinSources = 10, MaxSources = 5 };
        new JobsOptionsConsistencyValidator().Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void ZeroBudget_Fails() =>
        new JobsOptionsValidator().Validate(null, new JobsOptions { TokenBudget = 0 }).Failed.ShouldBeTrue();
}

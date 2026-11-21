using Microsoft.Extensions.Options;

namespace Research.Jobs.Application;

/// <summary>Bound from the <c>Jobs</c> section. Limits and clocks of every research job (§10.1).</summary>
public sealed class JobsOptions
{
    public const string SectionName = "Jobs";

    /// <summary>Hard deadline: an active job older than this becomes <c>Expired</c>.</summary>
    public TimeSpan MaxDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Time kept for synthesis: gathering ends at <c>MaxDuration − SynthesisReserve</c>.</summary>
    public TimeSpan SynthesisReserve { get; set; } = TimeSpan.FromSeconds(90);

    /// <summary>Indexed documents needed to synthesize; fewer fails the job.</summary>
    public int MinSources { get; set; } = 3;

    /// <summary>Most pages fetched per job.</summary>
    public int MaxSources { get; set; } = 12;

    public int MaxSubQuestions { get; set; } = 5;

    public int ResultsPerSearch { get; set; } = 6;

    /// <summary>LLM tokens per job; reaching it ends gathering (synthesis still runs).</summary>
    public long TokenBudget { get; set; } = 400_000;

    /// <summary>How long a step may run before recovery re-emits it.</summary>
    public TimeSpan StepLease { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>Emissions per step (first included) before recovery fails it with <c>research.step_exhausted</c>.</summary>
    public int MaxStepAttempts { get; set; } = 3;

    /// <summary>Passages Synthesis retrieves.</summary>
    public int SynthesisTopK { get; set; } = 12;

    public int QuestionMinLength { get; set; } = 10;

    public int QuestionMaxLength { get; set; } = 2000;

    public TimeSpan RecoveryInterval { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan DeadlineInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Jobs claimed per worker run.</summary>
    public int WorkerBatchSize { get; set; } = 50;

    public TimeSpan EventsPollInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Jobs allowed in an active state at once, across all replicas; a new job beyond it is refused with
    /// <c>503 jobs.capacity_reached</c>. Zero disables the cap. A soft cap: two creates racing may both pass (OWASP
    /// LLM10 unbounded consumption, ASI08 cascading failures).
    /// </summary>
    public int MaxActiveJobs { get; set; } = 50;

    /// <summary>Longest sub-question taken from a plan; longer ones are cut.</summary>
    public int MaxSubQuestionLength { get; set; } = 200;

    /// <summary>Longest report text stored; a longer one is cut and marked truncated.</summary>
    public int MaxReportChars { get; set; } = 200_000;

    /// <summary>Most citations stored with a report.</summary>
    public int MaxCitations { get; set; } = 100;
}

/// <summary>Checks each value of <see cref="JobsOptions"/>.</summary>
public sealed class JobsOptionsValidator : IValidateOptions<JobsOptions>
{
    public ValidateOptionsResult Validate(string? name, JobsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();

        if (options.MaxDuration <= TimeSpan.Zero)
        {
            failures.Add("Jobs:MaxDuration must be positive.");
        }

        if (options.SynthesisReserve <= TimeSpan.Zero)
        {
            failures.Add("Jobs:SynthesisReserve must be positive.");
        }

        if (options.MinSources < 1 || options.MaxSources < 1 || options.MaxSubQuestions < 1 || options.ResultsPerSearch < 1)
        {
            failures.Add("Jobs:MinSources, MaxSources, MaxSubQuestions and ResultsPerSearch must be at least 1.");
        }

        if (options.TokenBudget < 1 || options.MaxStepAttempts < 1 || options.SynthesisTopK < 1 || options.WorkerBatchSize < 1)
        {
            failures.Add("Jobs:TokenBudget, MaxStepAttempts, SynthesisTopK and WorkerBatchSize must be at least 1.");
        }

        if (options.StepLease <= TimeSpan.Zero || options.RecoveryInterval <= TimeSpan.Zero
            || options.DeadlineInterval <= TimeSpan.Zero || options.EventsPollInterval <= TimeSpan.Zero)
        {
            failures.Add("Jobs:StepLease and the worker intervals must be positive.");
        }

        if (options.QuestionMinLength < 1 || options.QuestionMaxLength < options.QuestionMinLength)
        {
            failures.Add("Jobs:QuestionMinLength must be at least 1 and not above QuestionMaxLength.");
        }

        if (options.MaxActiveJobs < 0)
        {
            failures.Add("Jobs:MaxActiveJobs cannot be negative (0 disables the cap).");
        }

        if (options.MaxSubQuestionLength < 20 || options.MaxReportChars < 1000 || options.MaxCitations < 1)
        {
            failures.Add("Jobs:MaxSubQuestionLength must be at least 20, MaxReportChars at least 1000 and MaxCitations at least 1.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

/// <summary>Checks that the values of <see cref="JobsOptions"/> fit together.</summary>
public sealed class JobsOptionsConsistencyValidator : IValidateOptions<JobsOptions>
{
    public ValidateOptionsResult Validate(string? name, JobsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();

        if (options.SynthesisReserve >= options.MaxDuration)
        {
            failures.Add("Jobs:SynthesisReserve must be shorter than Jobs:MaxDuration, or gathering never runs.");
        }

        if (options.MinSources > options.MaxSources)
        {
            failures.Add("Jobs:MinSources cannot exceed Jobs:MaxSources, or every job fails.");
        }

        if (options.DeadlineInterval >= options.SynthesisReserve)
        {
            failures.Add("Jobs:DeadlineInterval must be shorter than Jobs:SynthesisReserve, or the cutoff fires too late.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

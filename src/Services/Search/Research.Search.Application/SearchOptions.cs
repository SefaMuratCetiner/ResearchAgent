using Microsoft.Extensions.Options;

namespace Research.Search.Application;

/// <summary>Bound from the <c>Search</c> section.</summary>
public sealed class SearchOptions
{
    public const string SectionName = "Search";

    /// <summary>Upper bound on results per search, whatever the request asks.</summary>
    public int MaxResults { get; set; } = 10;

    /// <summary>How long results stay cached; zero disables the cache.</summary>
    public TimeSpan CacheTimeToLive { get; set; } = TimeSpan.FromHours(6);

    /// <summary>Concurrent provider calls per replica.</summary>
    public int MaxConcurrentSearches { get; set; } = 4;
}

/// <summary>Checks <see cref="SearchOptions"/> at startup.</summary>
public sealed class SearchOptionsValidator : IValidateOptions<SearchOptions>
{
    public ValidateOptionsResult Validate(string? name, SearchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (options.MaxResults < 1)
        {
            failures.Add("Search:MaxResults must be at least 1.");
        }

        if (options.CacheTimeToLive < TimeSpan.Zero)
        {
            failures.Add("Search:CacheTimeToLive cannot be negative.");
        }

        if (options.MaxConcurrentSearches < 1)
        {
            failures.Add("Search:MaxConcurrentSearches must be at least 1.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

using Research.SharedKernel.Results;

namespace Research.SharedKernel.Errors;

/// <summary>
/// Each library that defines error codes registers one contributor, so there is no central list to edit.
/// The error catalog validates all contributors at startup: codes unique and prefixed with their owner.
/// </summary>
public interface IErrorCatalogContributor
{
    /// <summary>The prefix every code of this contributor starts with, including the dot (<c>jobs.</c>).</summary>
    string Prefix { get; }

    IEnumerable<Error> GetErrors();
}

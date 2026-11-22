using Research.SharedKernel.Results;

namespace Research.Shared.Ports.Search;

/// <summary>A permanent provider rejection; the step fails with <see cref="Error"/> instead of being retried.</summary>
public sealed class SearchProviderException : Exception
{
    public SearchProviderException()
        : this(SearchErrors.ProviderRefused)
    {
    }

    public SearchProviderException(string message)
        : base(message)
    {
        Error = SearchErrors.ProviderRefused;
    }

    public SearchProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
        Error = SearchErrors.ProviderRefused;
    }

    public SearchProviderException(Error error, string? detail = null)
        : base(detail ?? error?.Title)
    {
        ArgumentNullException.ThrowIfNull(error);
        Error = error;
    }

    public Error Error { get; }
}

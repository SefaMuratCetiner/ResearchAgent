namespace Research.Fetching.Application.Common.Interfaces;

/// <summary>Limits concurrent downloads per host.</summary>
public interface IDomainThrottle
{
    Task<IDisposable> AcquireAsync(string host, CancellationToken cancellationToken);
}

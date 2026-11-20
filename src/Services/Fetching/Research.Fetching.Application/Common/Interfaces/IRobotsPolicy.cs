namespace Research.Fetching.Application.Common.Interfaces;

/// <summary>Checks whether fetching a URL is permitted by the target host's robots.txt policy.</summary>
public interface IRobotsPolicy
{
    Task<bool> IsAllowedAsync(Uri url, CancellationToken cancellationToken);
}

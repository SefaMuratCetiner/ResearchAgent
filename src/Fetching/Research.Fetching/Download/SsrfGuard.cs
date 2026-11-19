using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace Research.Fetching.Download;

/// <summary>
/// Server-side request forgery guard, installed as the <see cref="SocketsHttpHandler.ConnectCallback"/> of the
/// downloader: every connection (first request, every redirect hop, every retry) resolves the host itself and only
/// connects to a public address. Checking at connect time closes the DNS-rebinding gap of checking the URL first.
/// </summary>
public static class SsrfGuard
{
    public static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var host = context.DnsEndPoint.Host;
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);

        var allowed = addresses.Where(IsPublic).ToArray();
        if (allowed.Length == 0)
        {
            throw new BlockedAddressException(host);
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(allowed, context.DnsEndPoint.Port, cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>False for loopback, private, link-local, CGNAT, multicast, unspecified and other reserved ranges.</summary>
    public static bool IsPublic(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)
            || address.Equals(IPAddress.None) || address.Equals(IPAddress.IPv6None))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] == 0
                || b[0] == 10
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)       // CGNAT 100.64/10
                || b[0] == 127
                || (b[0] == 169 && b[1] == 254)                     // link-local, cloud metadata
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 0 && b[2] == 0)          // IETF protocol assignments
                || (b[0] == 192 && b[1] == 0 && b[2] == 2)          // TEST-NET-1
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 198 && (b[1] == 18 || b[1] == 19))      // benchmarking
                || (b[0] == 198 && b[1] == 51 && b[2] == 100)       // TEST-NET-2
                || (b[0] == 203 && b[1] == 0 && b[2] == 113)        // TEST-NET-3
                || b[0] >= 224);                                    // multicast and reserved
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = address.GetAddressBytes();
            return !(address.IsIPv6LinkLocal
                || address.IsIPv6SiteLocal
                || address.IsIPv6Multicast
                || (b[0] & 0xFE) == 0xFC                            // unique local fc00::/7
                || (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0D && b[3] == 0xB8)); // documentation 2001:db8::/32
        }

        return false;
    }
}

/// <summary>The host resolved only to addresses the guard refuses.</summary>
public sealed class BlockedAddressException : Exception
{
    public BlockedAddressException()
    {
    }

    public BlockedAddressException(string host)
        : base("The host resolves only to private or reserved addresses: " + host)
    {
    }

    public BlockedAddressException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

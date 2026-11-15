using System.Net;
using System.Net.Sockets;

namespace PageTrail.Worker.Fetching;

public static class PrivateAddressGuard
{
    public const string BlockedReason = "Blocked: host resolves to a private or local address";

    public static SocketsHttpHandler CreateHandler() => new() { ConnectCallback = ConnectAsync };

    public static bool IsBlocked(IPAddress address)
    {
        var ip = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        return ip.AddressFamily == AddressFamily.InterNetwork ? IsBlockedIPv4(ip.GetAddressBytes()) : IsBlockedIPv6(ip);
    }

    private static bool IsBlockedIPv4(byte[] b) =>
        b[0] == 0
        || b[0] == 10
        || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
        || (b[0] == 192 && b[1] == 168)
        || (b[0] == 169 && b[1] == 254);

    private static bool IsBlockedIPv6(IPAddress ip) =>
        ip.Equals(IPAddress.IPv6Any) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal;

    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        if (addresses.Length == 0 || addresses.Any(IsBlocked))
        {
            throw new BlockedAddressException();
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}

public sealed class BlockedAddressException() : Exception(PrivateAddressGuard.BlockedReason);

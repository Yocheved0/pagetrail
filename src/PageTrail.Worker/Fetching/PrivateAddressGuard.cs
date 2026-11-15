using System.Net;
using System.Net.Sockets;

namespace PageTrail.Worker.Fetching;

public static class PrivateAddressGuard
{
    public const string BlockedReason = "Blocked: host resolves to a private or local address";

    private const int IPv4Bytes = 4;
    private const int EmbeddingPrefixBytes = 12;
    private static readonly byte[] Nat64Prefix = [0x00, 0x64, 0xff, 0x9b, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] IPv4CompatiblePrefix = new byte[EmbeddingPrefixBytes];
    private static readonly byte[] SixToFourPrefix = [0x20, 0x02];

    public static SocketsHttpHandler CreateHandler() => CreateHandler(IsBlocked);

    public static SocketsHttpHandler CreateHandler(Func<IPAddress, bool> isBlocked) => new()
    {
        UseProxy = false,
        ConnectCallback = (context, cancellationToken) => ConnectAsync(context, isBlocked, cancellationToken),
    };

    public static bool IsBlocked(IPAddress address)
    {
        var ip = EmbeddedIPv4(address) ?? address;
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        return ip.AddressFamily == AddressFamily.InterNetwork ? IsBlockedIPv4(ip.GetAddressBytes()) : IsBlockedIPv6(ip);
    }

    private static IPAddress? EmbeddedIPv4(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return null;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            return address.MapToIPv4();
        }

        var bytes = address.GetAddressBytes();
        if (bytes.AsSpan(0, SixToFourPrefix.Length).SequenceEqual(SixToFourPrefix))
        {
            return new IPAddress(bytes.AsSpan(SixToFourPrefix.Length, IPv4Bytes));
        }

        var prefix = bytes.AsSpan(0, EmbeddingPrefixBytes);
        return prefix.SequenceEqual(Nat64Prefix) || prefix.SequenceEqual(IPv4CompatiblePrefix)
            ? new IPAddress(bytes.AsSpan(EmbeddingPrefixBytes))
            : null;
    }

    private static bool IsBlockedIPv4(byte[] b) =>
        b[0] == 0
        || b[0] == 10
        || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
        || (b[0] == 169 && b[1] == 254)
        || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
        || (b[0] == 192 && b[1] == 0 && b[2] == 0)
        || (b[0] == 192 && b[1] == 168)
        || (b[0] == 198 && b[1] is 18 or 19)
        || b[0] >= 224;

    private static bool IsBlockedIPv6(IPAddress ip) =>
        ip.Equals(IPAddress.IPv6Any) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal;

    private static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context, Func<IPAddress, bool> isBlocked, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        if (addresses.Length == 0 || addresses.Any(isBlocked))
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

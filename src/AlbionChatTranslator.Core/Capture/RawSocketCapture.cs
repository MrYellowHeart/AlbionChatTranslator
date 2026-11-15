using StatisticsAnalysisTool.Abstractions;
using System.Buffers.Binary;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace AlbionChatTranslator.Core.Capture;

/// <summary>
/// Reads Albion's UDP traffic with Windows raw sockets (SIO_RCVALL). Needs administrator
/// rights but no driver, so users install nothing extra. Read-only: it never sends,
/// modifies or injects packets.
/// </summary>
public sealed class RawSocketCapture : IDisposable
{
    /// <summary>Albion's Photon ports: 4535 chat server, 5055/5056/5058 game servers.</summary>
    public static readonly IReadOnlySet<ushort> AlbionPorts = new HashSet<ushort> { 4535, 5055, 5056, 5058 };

    private const int BufferSize = 65_535;

    private readonly IPhotonReceiver _receiver;
    private readonly Lock _deliverLock = new();
    private readonly List<Socket> _sockets = [];
    private readonly List<Task> _loops = [];
    private CancellationTokenSource? _cts;

    public RawSocketCapture(IPhotonReceiver receiver) =>
        _receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));

    public bool IsRunning => _sockets.Count > 0;

    /// <summary>Starts capturing on every active IPv4 interface. Returns the number of interfaces bound.</summary>
    public int Start()
    {
        if (IsRunning) return _sockets.Count;
        _cts = new CancellationTokenSource();

        foreach (var ip in LocalIPv4Addresses())
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.IP);
            try
            {
                socket.Bind(new IPEndPoint(ip, 0));
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, true);
                socket.IOControl(IOControlCode.ReceiveAll, BitConverter.GetBytes(1), new byte[4]);
                _sockets.Add(socket);
            }
            catch (SocketException)
            {
                // Typically "access denied" when not elevated, or an adapter that does not support RCVALL.
                socket.Dispose();
            }
        }

        foreach (var socket in _sockets)
            _loops.Add(Task.Run(() => ReceiveLoopAsync(socket, _cts.Token)));

        return _sockets.Count;
    }

    public void Stop()
    {
        _cts?.Cancel();
        foreach (var s in _sockets)
            s.Dispose();
        try { Task.WaitAll([.. _loops], TimeSpan.FromSeconds(2)); }
        catch (AggregateException) { }
        _sockets.Clear();
        _loops.Clear();
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose() => Stop();

    private async Task ReceiveLoopAsync(Socket socket, CancellationToken ct)
    {
        var buffer = new byte[BufferSize];
        while (!ct.IsCancellationRequested)
        {
            int read;
            try
            {
                read = await socket.ReceiveAsync(buffer, SocketFlags.None, ct).ConfigureAwait(false);
            }
            catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                if (ct.IsCancellationRequested) return;
                continue;
            }

            if (TryExtractAlbionPayload(buffer.AsSpan(0, read), out var start, out var length))
            {
                // PhotonParser keeps fragment state, so feed it from one thread at a time.
                lock (_deliverLock)
                {
                    try { _receiver.ReceivePacket(buffer.AsSpan(start, length)); }
                    catch (Exception e) { System.Diagnostics.Debug.WriteLine($"[Capture] parse error: {e.Message}"); }
                }
            }
        }
    }

    /// <summary>
    /// Finds the UDP payload inside an IPv4 frame if it is to or from an Albion port.
    /// Public for tests.
    /// </summary>
    public static bool TryExtractAlbionPayload(ReadOnlySpan<byte> frame, out int start, out int length)
    {
        start = length = 0;
        if (frame.Length < 20 || frame[0] >> 4 != 4) return false;

        int ihl = (frame[0] & 0x0F) * 4;
        if (ihl < 20 || frame.Length < ihl + 8) return false;
        if (frame[9] != 17) return false; // UDP only

        // Drop non-first IP fragments; Photon's own fragmentation covers large messages.
        if ((BinaryPrimitives.ReadUInt16BigEndian(frame[6..]) & 0x1FFF) != 0) return false;

        var udp = frame[ihl..];
        ushort srcPort = BinaryPrimitives.ReadUInt16BigEndian(udp);
        ushort dstPort = BinaryPrimitives.ReadUInt16BigEndian(udp[2..]);
        if (!AlbionPorts.Contains(srcPort) && !AlbionPorts.Contains(dstPort)) return false;

        int udpLength = BinaryPrimitives.ReadUInt16BigEndian(udp[4..]);
        int available = frame.Length - ihl - 8;
        int payload = Math.Min(available, udpLength - 8);
        if (payload <= 0) return false;

        start = ihl + 8;
        length = payload;
        return true;
    }

    private static IEnumerable<IPAddress> LocalIPv4Addresses()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                             ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                .Select(ua => ua.Address)
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .Distinct()
                .ToList();
        }
        catch (NetworkInformationException)
        {
            return [];
        }
    }
}

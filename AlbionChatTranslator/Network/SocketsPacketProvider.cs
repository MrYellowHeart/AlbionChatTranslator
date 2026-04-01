using StatisticsAnalysisTool.Abstractions;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace AlbionChatTranslator.Network;

public class SocketsPacketProvider : PacketProvider
{
    // 4535 = Albion chat server, 5055/5056/5058 = game zone server
    private static readonly HashSet<ushort> PhotonUdpPorts = [4535, 5055, 5056, 5058];

    private readonly IPhotonReceiver _photonReceiver;
    private readonly List<Socket> _socketsV4 = [];
    private readonly List<Socket> _socketsV6 = [];
    private readonly List<Task> _receiveTasks = [];
    private readonly ConcurrentBag<byte[]> _buffers = [];
    private volatile bool _stopReceiving;

    public SocketsPacketProvider(IPhotonReceiver photonReceiver)
    {
        _photonReceiver = photonReceiver ?? throw new ArgumentNullException(nameof(photonReceiver));
    }

    public override bool IsRunning =>
        _socketsV4.Any(s => s is { IsBound: true }) ||
        _socketsV6.Any(s => s is { IsBound: true });

    public override void Start()
    {
        _stopReceiving = false;

        var v4 = GetLocalUnicastAddresses(AddressFamily.InterNetwork).ToList();
        var v6 = GetLocalUnicastAddresses(AddressFamily.InterNetworkV6).ToList();

        if (v4.Count == 0 && v6.Count == 0) return;

        foreach (var ip in v4) CreateRawSocketIPv4(ip);
        foreach (var ip in v6) CreateRawSocketIPv6(ip);

        foreach (var s in _socketsV4.Concat(_socketsV6))
        {
            var buffer = RentBuffer();
            _receiveTasks.Add(Task.Run(() => ReceiveLoopAsync(s, buffer)));
        }
    }

    public override void Stop()
    {
        _stopReceiving = true;

        foreach (var s in _socketsV4.Concat(_socketsV6))
            SafeClose(s);

        _socketsV4.Clear();
        _socketsV6.Clear();

        try { Task.WaitAll([.. _receiveTasks], TimeSpan.FromSeconds(2)); }
        catch { }

        _receiveTasks.Clear();
        while (_buffers.TryTake(out _)) { }
    }

    private void CreateRawSocketIPv4(IPAddress ip)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.IP);
        try
        {
            socket.Bind(new IPEndPoint(ip, 0));
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, true);
            socket.IOControl(IOControlCode.ReceiveAll, [1, 0, 0, 0], new byte[4]);
            _socketsV4.Add(socket);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[SocketsPacketProvider] IPv4 bind failed on {ip}: {e.Message}");
            SafeClose(socket);
        }
    }

    private void CreateRawSocketIPv6(IPAddress ip)
    {
        var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Raw, ProtocolType.IPv6);
        try
        {
            socket.Bind(new IPEndPoint(ip, 0));
            try { socket.SetSocketOption(SocketOptionLevel.IPv6, (SocketOptionName)29, true); }
            catch { }
            _socketsV6.Add(socket);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[SocketsPacketProvider] IPv6 bind failed on {ip}: {e.Message}");
            SafeClose(socket);
        }
    }

    private async Task ReceiveLoopAsync(Socket socket, byte[] buffer)
    {
        while (!_stopReceiving)
        {
            try
            {
                int bytes = await socket.ReceiveAsync(buffer, SocketFlags.None).ConfigureAwait(false);
                if (bytes > 0)
                    ProcessFrame(buffer.AsSpan(0, bytes), socket.AddressFamily);
            }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) when (_stopReceiving) { break; }
            catch (Exception e) { Debug.WriteLine($"[SocketsPacketProvider] Receive error: {e.Message}"); }
        }

        ReturnBuffer(buffer);
    }

    private void ProcessFrame(ReadOnlySpan<byte> frame, AddressFamily af)
    {
        if (af == AddressFamily.InterNetwork) ProcessIPv4(frame);
        else if (af == AddressFamily.InterNetworkV6) ProcessIPv6(frame);
    }

    private void ProcessIPv4(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 20) return;

        byte verIhl = frame[0];
        if ((verIhl >> 4) != 4) return;

        int ihl = (verIhl & 0x0F) * 4;
        if (ihl < 20 || frame.Length < ihl) return;

        int fragOffset = (BinaryPrimitives.ReadUInt16BigEndian(frame.Slice(6, 2)) & 0x1FFF) * 8;
        if (fragOffset != 0) return;

        if (frame[9] != 17) return;
        if (frame.Length < ihl + 8) return;

        var udp = frame[ihl..];
        ushort srcPort = BinaryPrimitives.ReadUInt16BigEndian(udp);
        ushort dstPort = BinaryPrimitives.ReadUInt16BigEndian(udp[2..]);
        ushort udpLen  = BinaryPrimitives.ReadUInt16BigEndian(udp[4..]);

        int po  = ihl + 8;
        int max = frame.Length - po;
        int len = Math.Min(max, Math.Max(0, udpLen - 8));
        if (len <= 0) return;

        var payload = frame.Slice(po, len);

        if (!PhotonUdpPorts.Contains(srcPort) && !PhotonUdpPorts.Contains(dstPort))
        {
            if (!LooksLikePhoton(payload)) return;
        }

        Deliver(payload);
    }

    private void ProcessIPv6(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 40) return;
        if (((frame[0] >> 4) & 0x0F) != 6) return;

        ushort payloadLen = BinaryPrimitives.ReadUInt16BigEndian(frame.Slice(4, 2));
        if (frame[6] != 17) return;

        const int l3 = 40;
        if (frame.Length < l3 + 8) return;

        var udp = frame[l3..];
        ushort srcPort = BinaryPrimitives.ReadUInt16BigEndian(udp);
        ushort dstPort = BinaryPrimitives.ReadUInt16BigEndian(udp[2..]);
        ushort udpLen  = BinaryPrimitives.ReadUInt16BigEndian(udp[4..]);

        int po  = l3 + 8;
        if (udpLen == 0 || udpLen > payloadLen || frame.Length < po) return;

        int len = Math.Min(frame.Length - po, Math.Max(0, udpLen - 8));
        if (len <= 0) return;

        var payload = frame.Slice(po, len);
        if (!PhotonUdpPorts.Contains(srcPort) && !PhotonUdpPorts.Contains(dstPort))
        {
            if (!LooksLikePhoton(payload)) return;
        }

        Deliver(payload);
    }

    private void Deliver(ReadOnlySpan<byte> payload)
    {
        if (payload.Length == 0) return;
        try { _photonReceiver.ReceivePacket(payload); }
        catch { }
    }

    private static bool LooksLikePhoton(ReadOnlySpan<byte> payload) =>
        payload.Length >= 3 && payload[0] is 0xF1 or 0xF2 or 0xFE;

    private static IEnumerable<IPAddress> GetLocalUnicastAddresses(AddressFamily family)
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                             ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                .Select(ua => ua.Address)
                .Where(a => a.AddressFamily == family)
                .Distinct();
        }
        catch { return []; }
    }

    private static void SafeClose(Socket s)
    {
        try { if (s.Connected) s.Shutdown(SocketShutdown.Both); } catch { }
        try { s.Close(); } catch { }
        try { s.Dispose(); } catch { }
    }

    private byte[] RentBuffer()
    {
        if (_buffers.TryTake(out var buf)) return buf;
        return new byte[65535];
    }

    private void ReturnBuffer(byte[] buf)
    {
        if (buf.Length == 65535) _buffers.Add(buf);
    }
}

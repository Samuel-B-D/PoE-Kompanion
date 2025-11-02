namespace PoEKompanion;

using System;
using System.Buffers;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;

public sealed class UnixSocketIpc : IDisposable
{
    private const string SERVER_SOCKET_PATH = "/tmp/poe-kompanion-server.sock";
    private const string CLIENT_SOCKET_PATH = "/tmp/poe-kompanion-client.sock";
    private readonly Socket socket;
    private readonly string? localPath;
    private readonly UnixDomainSocketEndPoint? remoteEndPoint;

    private UnixSocketIpc(Socket socket, string? localPath, UnixDomainSocketEndPoint? remoteEndPoint)
    {
        this.socket = socket;
        this.localPath = localPath;
        this.remoteEndPoint = remoteEndPoint;
    }

    public static async Task<UnixSocketIpc> CreateServerAsync()
    {
        if (File.Exists(SERVER_SOCKET_PATH))
        {
            File.Delete(SERVER_SOCKET_PATH);
        }

        var socket = new Socket(AddressFamily.Unix, SocketType.Dgram, ProtocolType.Unspecified);
        socket.Bind(new UnixDomainSocketEndPoint(SERVER_SOCKET_PATH));

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            // ReSharper disable once UseObjectOrCollectionInitializer
            var serverSocketInfo = new FileInfo(SERVER_SOCKET_PATH);
            serverSocketInfo.UnixFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite |
                                            UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
                                            UnixFileMode.OtherRead | UnixFileMode.OtherWrite;
        }

        while (!File.Exists(CLIENT_SOCKET_PATH))
        {
            await Task.Yield();
        }

        var clientEndPoint = new UnixDomainSocketEndPoint(CLIENT_SOCKET_PATH);
        return new UnixSocketIpc(socket, SERVER_SOCKET_PATH, clientEndPoint);
    }

    public static async Task<UnixSocketIpc> CreateClientAsync()
    {
        if (File.Exists(CLIENT_SOCKET_PATH))
        {
            File.Delete(CLIENT_SOCKET_PATH);
        }

        var socket = new Socket(AddressFamily.Unix, SocketType.Dgram, ProtocolType.Unspecified);
        socket.Bind(new UnixDomainSocketEndPoint(CLIENT_SOCKET_PATH));

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            // ReSharper disable once UseObjectOrCollectionInitializer
            var clientSocketInfo = new FileInfo(CLIENT_SOCKET_PATH);
            clientSocketInfo.UnixFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite |
                                            UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
                                            UnixFileMode.OtherRead | UnixFileMode.OtherWrite;
        }

        while (!File.Exists(SERVER_SOCKET_PATH))
        {
            await Task.Yield();
        }

        var serverEndPoint = new UnixDomainSocketEndPoint(SERVER_SOCKET_PATH);
        return new UnixSocketIpc(socket, CLIENT_SOCKET_PATH, serverEndPoint);
    }

    public async Task SendAsync(IpcMessage message, CancellationToken cancellationToken = default)
    {
        if (this.remoteEndPoint is null) return;

        var buffer = MessagePackSerializer.Serialize(message, cancellationToken: cancellationToken);
        Console.WriteLine($"Sending IPC message: {message.GetType().Name} ({buffer.Length} bytes)");

        if (buffer.Length > 8192)
        {
            Console.WriteLine($"WARNING: Message size {buffer.Length} bytes exceeds typical datagram limits!");
        }

        await this.socket.SendToAsync(buffer, SocketFlags.None, this.remoteEndPoint, cancellationToken);
    }

    public async Task<IpcMessage?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        var buffer = new byte[16384];
        var received = await this.socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);

        if (received == 0) return null;

        Console.WriteLine($"Received {received} bytes via IPC");
        return MessagePackSerializer.Deserialize<IpcMessage>(buffer.AsMemory(0, received), cancellationToken: cancellationToken);
    }

    public void Dispose()
    {
        this.socket.Dispose();

        if (this.localPath is not null && File.Exists(this.localPath))
        {
            File.Delete(this.localPath);
        }
    }
}

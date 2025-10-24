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
    private const string ServerSocketPath = "/tmp/poe-kompanion-server.sock";
    private const string ClientSocketPath = "/tmp/poe-kompanion-client.sock";
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
        if (File.Exists(ServerSocketPath))
        {
            File.Delete(ServerSocketPath);
        }

        var socket = new Socket(AddressFamily.Unix, SocketType.Dgram, ProtocolType.Unspecified);
        socket.Bind(new UnixDomainSocketEndPoint(ServerSocketPath));

        var serverSocketInfo = new FileInfo(ServerSocketPath);
        serverSocketInfo.UnixFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite |
                                        UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
                                        UnixFileMode.OtherRead | UnixFileMode.OtherWrite;

        while (!File.Exists(ClientSocketPath))
        {
            await Task.Yield();
        }

        var clientEndPoint = new UnixDomainSocketEndPoint(ClientSocketPath);
        return new UnixSocketIpc(socket, ServerSocketPath, clientEndPoint);
    }

    public static async Task<UnixSocketIpc> CreateClientAsync()
    {
        if (File.Exists(ClientSocketPath))
        {
            File.Delete(ClientSocketPath);
        }

        var socket = new Socket(AddressFamily.Unix, SocketType.Dgram, ProtocolType.Unspecified);
        socket.Bind(new UnixDomainSocketEndPoint(ClientSocketPath));

        var clientSocketInfo = new FileInfo(ClientSocketPath);
        clientSocketInfo.UnixFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite |
                                        UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
                                        UnixFileMode.OtherRead | UnixFileMode.OtherWrite;

        while (!File.Exists(ServerSocketPath))
        {
            await Task.Yield();
        }

        var serverEndPoint = new UnixDomainSocketEndPoint(ServerSocketPath);
        return new UnixSocketIpc(socket, ClientSocketPath, serverEndPoint);
    }

    public async Task SendAsync(IpcMessage message, CancellationToken cancellationToken = default)
    {
        if (this.remoteEndPoint is null) return;

        var buffer = MessagePackSerializer.Serialize(message);
        await this.socket.SendToAsync(buffer, SocketFlags.None, this.remoteEndPoint, cancellationToken);
    }

    public async Task<IpcMessage?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        var buffer = new byte[4096];
        var received = await this.socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);

        if (received == 0) return null;

        return MessagePackSerializer.Deserialize<IpcMessage>(buffer.AsMemory(0, received));
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

namespace PoEKompanion;

using System.Threading;
using System.Threading.Tasks;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

internal sealed class PoETracker
{
    public static readonly PoETracker Instance = new();

    private const int UINPUT_MAX_NAME_SIZE = 80;
    private const uint UI_SET_EVBIT = 0x40045564;
    private const uint UI_SET_KEYBIT = 0x40045565;
    private const uint UI_DEV_SETUP = 0x405C5503;
    private const uint UI_DEV_CREATE = 0x5501;
    private const uint UI_DEV_DESTROY = 0x5502;
    private const int EV_KEY = 0x01;
    private const int EV_SYN = 0x00;
    private const int SYN_REPORT = 0;

    [DllImport("libc", SetLastError = true)]
    private static extern int open(string pathname, int flags);

    [DllImport("libc", SetLastError = true)]
    private static extern int close(int fd);

    [DllImport("libc", SetLastError = true)]
    private static extern int ioctl(int fd, uint request, int value);

    [DllImport("libc", SetLastError = true)]
    private static extern int ioctl(int fd, uint request, ref UinputSetup setup);

    [DllImport("libc", SetLastError = true)]
    private static extern int write(int fd, byte[] buffer, int count);

    [DllImport("libc", SetLastError = true)]
    private static extern IntPtr strerror(int errnum);

    [StructLayout(LayoutKind.Sequential)]
    private struct InputEvent
    {
        public long TimeSec;
        public long TimeUsec;
        public ushort Type;
        public ushort Code;
        public int Value;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    private struct UinputSetup
    {
        public UinputId Id;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = UINPUT_MAX_NAME_SIZE)]
        public byte[] Name;
        public uint FfEffectsMax;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct UinputId
    {
        public ushort BusType;
        public ushort Vendor;
        public ushort Product;
        public ushort Version;
    }

    // ReSharper disable once NotAccessedField.Local
    private readonly Timer hookTimer;

    private Process? poeProcess;
    private readonly List<OpenedPoEConnection> openedPoEConnections = [];
    private UnixSocketIpc? ipc;
    private int virtualKeyboardFd = -1;

    private PoETracker()
    {
        this.hookTimer = new Timer(_ =>
        {
            Task.Run(async () =>
            {
                this.FindPoEExecutable();
                await this.FindGameConnections();
            });
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    public async Task RunAsync(int parentProcessId)
    {
        this.ipc = await UnixSocketIpc.CreateClientAsync();

        if (parentProcessId > 0)
        {
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(1000);

                    try
                    {
                        Process.GetProcessById(parentProcessId);
                    }
                    catch (ArgumentException)
                    {
                        Console.WriteLine("Parent process died, exiting...");
                        Environment.Exit(0);
                    }
                }
            });
        }

        this.InitializeVirtualKeyboard();
        await this.ipc.SendAsync(new BackgroundReadyMessage());
        Console.WriteLine("Background process ready, notified foreground");

        while (true)
        {
            var message = await this.ipc.ReceiveAsync();
            Console.WriteLine($"Received IPC message: {message}");

            if (message is ForceLogoutMessage)
            {
                await this.CloseGameConnections();
            }
            else if (message is ChatCommandMessage chatCommand)
            {
                await this.SendChatCommand(chatCommand.Command);
            }
            else if (message is KeyboardLayoutMapMessage layoutMapMessage)
            {
                SetLayoutMap(layoutMapMessage.LayoutMap);
            }
        }
        // ReSharper disable once FunctionNeverReturns
    }

    private void FindPoEExecutable()
    {
        // Filter for process with PathOfExileSteam in command line
        var proc = Process.GetProcesses().FirstOrDefault(p => p.ProcessName.StartsWith("PathOfExileSte"));
        var oldProcId = this.poeProcess?.Id;
        this.poeProcess = proc;

        if (oldProcId != proc?.Id)
        {
            Console.WriteLine($"PoE Process ID: {proc?.Id}");

            if (proc?.Id is not null)
            {
                _ = this.ipc?.SendAsync(new PoEHookedMessage(proc.Id));
                _ = this.ipc?.SendAsync(new SetAlwaysOnTopMessage(proc.Id));
            }
            else
            {
                _ = this.ipc?.SendAsync(new PoEUnhookedMessage());
            }
        }
    }

    private async Task FindGameConnections()
    {
        if (this.poeProcess is null) return;

        var pid = this.poeProcess.Id;
        var inodes = await GetInodesForPid(pid);

        var newOpenedConnections = new List<OpenedPoEConnection>();
        await foreach (var openedConn in FindGameOpenedConnections($"/proc/{pid}/net/tcp", inodes, "TCP"))
        {
            newOpenedConnections.Add(openedConn);
        }

        foreach (var openedConn in newOpenedConnections)
        {
            if (!this.openedPoEConnections.Contains(openedConn))
            {
                Console.WriteLine($"New PoE Connection: {openedConn}");
                this.openedPoEConnections.Add(openedConn);
            }
        }

        var connsToRemove = new List<OpenedPoEConnection>();
        foreach (var existingConn in this.openedPoEConnections)
        {
            if (!newOpenedConnections.Contains(existingConn))
            {
                Console.WriteLine($"PoE Connection gone: {existingConn}");
                connsToRemove.Add(existingConn);
            }
        }

        foreach (var connToRemove in connsToRemove)
        {
            this.openedPoEConnections.Remove(connToRemove);
        }
    }

    private async Task CloseGameConnections()
    {
        foreach (var connection in this.openedPoEConnections)
        {
            SendRstPacket(connection);
        }

        this.FindPoEExecutable();
        await this.FindGameConnections();

        foreach (var connection in this.openedPoEConnections)
        {
            SendRstPacket(connection);
        }

        Console.WriteLine("Killed All PoE Connections");
        this.openedPoEConnections.Clear();
    }

    private void InitializeVirtualKeyboard()
    {
        if (this.virtualKeyboardFd >= 0) return;

        const int O_WRONLY = 1;
        const int O_NONBLOCK = 0x800;

        this.virtualKeyboardFd = open("/dev/uinput", O_WRONLY | O_NONBLOCK);
        if (this.virtualKeyboardFd < 0)
        {
            var errno = Marshal.GetLastWin32Error();
            var errMsg = Marshal.PtrToStringAnsi(strerror(errno));
            Console.WriteLine($"Failed to open /dev/uinput: {errMsg} (errno={errno})");
            return;
        }

        Console.WriteLine("Successfully opened /dev/uinput");

        ioctl(this.virtualKeyboardFd, UI_SET_EVBIT, EV_KEY);
        ioctl(this.virtualKeyboardFd, UI_SET_EVBIT, EV_SYN);

        for (var i = 0; i < 256; i++)
        {
            ioctl(this.virtualKeyboardFd, UI_SET_KEYBIT, i);
        }

        var nameBytes = new byte[UINPUT_MAX_NAME_SIZE];
        var nameStr = "PoE Kompanion Virtual Keyboard";
        System.Text.Encoding.ASCII.GetBytes(nameStr, 0, Math.Min(nameStr.Length, UINPUT_MAX_NAME_SIZE - 1), nameBytes, 0);

        var setup = new UinputSetup
        {
            Id = new UinputId
            {
                BusType = 0x03,
                Vendor = 0x1234,
                Product = 0x5678,
                Version = 1
            },
            Name = nameBytes,
            FfEffectsMax = 0
        };

        var ret = ioctl(this.virtualKeyboardFd, UI_DEV_SETUP, ref setup);
        if (ret < 0)
        {
            var errno = Marshal.GetLastWin32Error();
            var errMsg = Marshal.PtrToStringAnsi(strerror(errno));
            Console.WriteLine($"UI_DEV_SETUP failed: {errMsg} (errno={errno})");
        }

        ret = ioctl(this.virtualKeyboardFd, UI_DEV_CREATE, 0);
        if (ret < 0)
        {
            var errno = Marshal.GetLastWin32Error();
            var errMsg = Marshal.PtrToStringAnsi(strerror(errno));
            Console.WriteLine($"UI_DEV_CREATE failed: {errMsg} (errno={errno})");
            close(this.virtualKeyboardFd);
            this.virtualKeyboardFd = -1;
            return;
        }

        Console.WriteLine("Virtual keyboard created successfully");
    }

    private async Task SendChatCommand(string command)
    {
        if (this.poeProcess is null)
        {
            Console.WriteLine("Cannot send chat command: PoE process not found");
            return;
        }

        this.InitializeVirtualKeyboard();

        if (this.virtualKeyboardFd < 0)
        {
            Console.WriteLine("Virtual keyboard not available");
            return;
        }

        try
        {
            Console.WriteLine($"Attempting to send chat command: {command}");

            SendKey(this.virtualKeyboardFd, KEY_ENTER, true);
            SendKey(this.virtualKeyboardFd, KEY_ENTER, false);

            await Task.Delay(10);

            foreach (var c in command)
            {
                SendCharWithLayout(this.virtualKeyboardFd, c);
                await Task.Delay(1);
            }

            await Task.Delay(10);
            
            SendKey(this.virtualKeyboardFd, KEY_ENTER, true);
            SendKey(this.virtualKeyboardFd, KEY_ENTER, false);

            Console.WriteLine($"Successfully sent chat command: {command}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending chat command: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private static void SendKey(int fd, int keyCode, bool press)
    {
        var evt = new InputEvent
        {
            TimeSec = 0,
            TimeUsec = 0,
            Type = EV_KEY,
            Code = (ushort)keyCode,
            Value = press ? 1 : 0
        };

        var evtBytes = new byte[Marshal.SizeOf<InputEvent>()];
        var handle = GCHandle.Alloc(evtBytes, GCHandleType.Pinned);
        Marshal.StructureToPtr(evt, handle.AddrOfPinnedObject(), false);
        var ret = write(fd, evtBytes, evtBytes.Length);
        handle.Free();

        if (ret < 0)
        {
            Console.WriteLine($"Failed to write key event (keycode={keyCode}, press={press})");
        }

        var synEvt = new InputEvent
        {
            TimeSec = 0,
            TimeUsec = 0,
            Type = EV_SYN,
            Code = SYN_REPORT,
            Value = 0
        };

        var synBytes = new byte[Marshal.SizeOf<InputEvent>()];
        handle = GCHandle.Alloc(synBytes, GCHandleType.Pinned);
        Marshal.StructureToPtr(synEvt, handle.AddrOfPinnedObject(), false);
        write(fd, synBytes, synBytes.Length);
        handle.Free();
    }

    private const int KEY_LEFTSHIFT = 42;
    private const int KEY_LEFTCTRL = 29;
    private const int KEY_ENTER = 28;

    private static Dictionary<char, KeycodMapping>? layoutMap;

    private static void SetLayoutMap(Dictionary<char, KeycodMapping> map)
    {
        layoutMap = map;
        Console.WriteLine($"Received layout map with {layoutMap.Count} character mappings");
    }

    private static Dictionary<char, KeycodMapping> GetLayoutMap()
    {
        if (layoutMap != null) return layoutMap;

        Console.WriteLine("Warning: Layout map not yet received from foreground process, using empty map");
        return new Dictionary<char, KeycodMapping>();
    }

    private static void SendCharWithLayout(int fd, char c)
    {
        var map = GetLayoutMap();

        if (!map.TryGetValue(c, out var mapping))
        {
            Console.WriteLine($"Warning: No keycode mapping for character '{c}'");
            return;
        }

        var keyCode = mapping.Keycode;
        var needsShift = mapping.Shift;


        if (needsShift)
        {
            SendKey(fd, KEY_LEFTSHIFT, true);
        }

        SendKey(fd, keyCode, true);
        SendKey(fd, keyCode, false);

        if (needsShift)
        {
            SendKey(fd, KEY_LEFTSHIFT, false);
        }
    }

    private static void SendRstPacket(OpenedPoEConnection openedPoEConnection)
    {
        try
        {
            // Use ss to kill the connection
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = $"-c \"sudo ss -K dst {openedPoEConnection.RemoteAddr} dport {openedPoEConnection.RemotePort}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
            };
            proc.Start();
            proc.WaitForExit();

            Console.WriteLine($"Closed connection to {openedPoEConnection.RemoteAddr}:{openedPoEConnection.RemotePort}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: {ex.Message}");
        }
    }

    private static async Task<HashSet<string>> GetInodesForPid(int pid)
    {
        var inodes = new HashSet<string>();
        var inodesLock = new Lock();
        var fdPath = $"/proc/{pid}/fd";

        if (!Directory.Exists(fdPath))
        {
            Console.WriteLine($"FD path not found: {fdPath}");
            return inodes;
        }

        try
        {
            var files = Directory.GetFiles(fdPath);

            await Parallel.ForEachAsync(files, async (file, ct) =>
            {
                try
                {
                    var proc = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "readlink",
                            Arguments = file,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true,
                        },
                    };
                    proc.Start();
                    var link = (await proc.StandardOutput.ReadToEndAsync(ct)).Trim();
                    await proc.WaitForExitAsync(ct);

                    if (link.StartsWith("socket:[") && link.EndsWith(']'))
                    {
                        var inode = link.Substring(8, link.Length - 9);
                        inodesLock.Enter();
                        inodes.Add(inode);
                        inodesLock.Exit();
                    }
                }
                catch (Exception) { /* nom */ }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        return inodes;
    }

    private static async IAsyncEnumerable<OpenedPoEConnection> FindGameOpenedConnections(string path, HashSet<string> inodes, string protocol)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"{protocol}: File not found: {path}");
            yield break;
        }

        var first = true;
        await foreach (var line in File.ReadLinesAsync(path))
        {
            if (first)
            {
                first = false;
                continue;
            }
            
            var parts = line.Split([' '], StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 10) continue;

            var state = parts[3];
            var inode = parts[9];

            // Only show ESTABLISHED connections with matching inodes
            if (state != "01" || !inodes.Contains(inode)) continue;

            // var localAddr = HexToIp(parts[1].Split(':')[0]);
            // var localPort = int.Parse(parts[1].Split(':')[1], System.Globalization.NumberStyles.HexNumber);
            string? localAddr = null;
            int? localPort = null;
            var remoteAddr = HexToIp(parts[2].Split(':')[0]);
            var remotePort = int.Parse(parts[2].Split(':')[1], System.Globalization.NumberStyles.HexNumber);

            // Only show connections to non-localhost addresses
            if (!remoteAddr.StartsWith("127.") && remoteAddr != "0.0.0.0")
            {
                yield return new OpenedPoEConnection(protocol, localAddr, localPort, remoteAddr, remotePort);
            }
        }
    }

    private static string HexToIp(string hex)
    {
        var bytes = new byte[4];
        for (var i = 0; i < 4; ++i)
        {
            bytes[3 - i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        return string.Join(".", bytes);
    }

    // ReSharper disable NotAccessedPositionalProperty.Local
    private sealed record OpenedPoEConnection(string Protocol, string? LocalAddr, int? LocalPort, string RemoteAddr, int RemotePort)
    {
        public override string ToString()
        {
            return $"PoE Opened connection ({this.Protocol}): {this.LocalAddr}:{this.LocalPort} -> {this.RemoteAddr}:{this.RemotePort}";
        }
    }
}
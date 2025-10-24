namespace PoEKompanion;

using System.Threading;
using System.Threading.Tasks;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

internal sealed class PoETracker
{
    public static readonly PoETracker Instance = new();

    // ReSharper disable once NotAccessedField.Local
    private readonly Timer hookTimer;

    private Process? poeProcess;
    private readonly List<OpenedPoEConnection> openedPoEConnections = [];
    private UnixSocketIpc? ipc;

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

        while (true)
        {
            var message = await this.ipc.ReceiveAsync();

            if (message is ForceLogoutMessage)
            {
                await this.CloseGameConnections();
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
                _ = this.ipc?.SendAsync(new NotificationMessage("Path of Exile process detected and hooked!", false));
                _ = this.ipc?.SendAsync(new SetAlwaysOnTopMessage(proc.Id));
            }
            else
            {
                this.ipc?.SendAsync(new NotificationMessage("Path of Exile closed", false));
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
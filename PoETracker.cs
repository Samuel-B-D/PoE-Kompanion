using System.Threading;
using System.Threading.Tasks;

namespace PoELogoutMacro;

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

    public async Task RunAsync()
    {
        while (true)
        {
            var c = (char)Console.Read();
            Console.WriteLine(c);
            await ((DispatchedActions)c switch
            {
                DispatchedActions.ForceLogout => this.CloseGameConnections(),
                _ => Task.CompletedTask,
            });
        }
        // ReSharper disable once FunctionNeverReturns
    }

    private void FindPoEExecutable()
    {
        // Filter for process with PathOfExileSteam in command line
        var proc = Process.GetProcesses().FirstOrDefault(p => p.ProcessName.StartsWith("PathOfExileSte"));

        if (proc == null) return;

        this.poeProcess = proc;
    }

    async Task FindGameConnections()
    {
        if (this.poeProcess is null) return;

        var pid = this.poeProcess.Id;
        var inodes = GetInodesForPid(pid);

        this.openedPoEConnections.Clear();
        await foreach (var openedConn in FindGameOpenedConnections($"/proc/{pid}/net/tcp", inodes, "TCP"))
        {
            this.openedPoEConnections.Add(openedConn);
        }
        // this.openedPoEConnections.AddRange(FindGameOpenedConnections($"/proc/{pid}/net/tcp", inodes, "TCP"));
    }

    async Task CloseGameConnections()
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
    }

    static void SendRstPacket(OpenedPoEConnection openedPoEConnection)
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
                }
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

    static HashSet<string> GetInodesForPid(int pid)
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

            Parallel.ForEach(files, file =>
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
                        }
                    };
                    proc.Start();
                    var link = proc.StandardOutput.ReadToEnd().Trim();
                    proc.WaitForExit();

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

    static async IAsyncEnumerable<OpenedPoEConnection> FindGameOpenedConnections(string path, HashSet<string> inodes, string protocol)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"{protocol}: File not found: {path}");
            yield break;
        }

        bool first = true;
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
                Console.WriteLine($"PoE Opened connection ({protocol}): {localAddr}:{localPort} -> {remoteAddr}:{remotePort}");
                yield return new OpenedPoEConnection(localAddr, localPort, remoteAddr, remotePort);
            }
        }
    }

    static string HexToIp(string hex)
    {
        var bytes = new byte[4];
        for (var i = 0; i < 4; ++i)
        {
            bytes[3 - i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        return string.Join(".", bytes);
    }

    // ReSharper disable NotAccessedPositionalProperty.Local
    private sealed record OpenedPoEConnection(string? LocalAddr, int? LocalPort, string RemoteAddr, int RemotePort);
}
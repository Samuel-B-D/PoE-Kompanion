using System.Threading;
using System.Threading.Tasks;
using SharpHook;
using SharpHook.Data;

namespace PoELogoutMacro;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

internal sealed class PoETracker
{
    public static PoETracker? Instance;

    private readonly Timer hookTimer;

    public static void Initialize()
    {
        _ = Instance = new();
    }

    public static void InitializeBlocking()
    {
        _ = Instance = new(true);
    }

    private Process? poeProcess;
    private List<OpenedPoEConnection> openedPoEConnections = [];

    public PoETracker(bool block = false)
    {
        this.hookTimer = new Timer(_ =>
        {
            this.FindPoEExecutable();
            this.FindGameConnections();
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));

        while (true)
        {
            var c = (char)Console.Read();
            Console.WriteLine(c);
            ((DispatchedActions)c switch
            {
                DispatchedActions.ForceLogout => (Action)this.CloseGameConnections,
                _ => delegate { }
            })();
        }
    }

    private void FindPoEExecutable()
    {
        // Filter for process with PathOfExileSteam in command line
        var poeProcess = Process.GetProcesses().FirstOrDefault(p => p.ProcessName.StartsWith("PathOfExileSte"));

        if (poeProcess == null) return;

        this.poeProcess = poeProcess;
    }

    void FindGameConnections()
    {
        if (this.poeProcess is null) return;

        var pid = this.poeProcess.Id;
        var inodes = GetInodesForPid(pid);

        this.openedPoEConnections.Clear();
        this.openedPoEConnections.AddRange(FindGameOpenedConnections($"/proc/{pid}/net/tcp", inodes, "TCP"));
    }

    void CloseGameConnections()
    {
        foreach (var connection in this.openedPoEConnections)
        {
            SendRstPacket(connection);
        }

        this.FindPoEExecutable();
        this.FindGameConnections();
        
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
                    Arguments = $"-c \"sudo ss -K dst {openedPoEConnection.remoteAddr} dport {openedPoEConnection.remotePort}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                }
            };
            proc.Start();
            proc.WaitForExit();

            Console.WriteLine($"Closed connection to {openedPoEConnection.remoteAddr}:{openedPoEConnection.remotePort}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: {ex.Message}");
        }
    }

    static HashSet<string> GetInodesForPid(int pid)
    {
        var inodes = new HashSet<string>();
        var fdPath = $"/proc/{pid}/fd";

        if (!Directory.Exists(fdPath))
        {
            Console.WriteLine($"FD path not found: {fdPath}");
            return inodes;
        }

        try
        {
            var files = Directory.GetFiles(fdPath);

            foreach (var file in files)
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
                        inodes.Add(inode);
                    }
                }
                catch
                {
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        return inodes;
    }

    static IEnumerable<OpenedPoEConnection> FindGameOpenedConnections(string path, HashSet<string> inodes,
        string protocol)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"{protocol}: File not found: {path}");
            yield break;
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading {path}: {ex.Message}");
            yield break;
        }

        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split([' '], StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 10)
                continue;

            var state = parts[3];
            var inode = parts[9];

            // Only show ESTABLISHED connections with matching inodes
            if (state != "01" || !inodes.Contains(inode))
                continue;

            var localAddr = HexToIp(parts[1].Split(':')[0]);
            var localPort = int.Parse(parts[1].Split(':')[1], System.Globalization.NumberStyles.HexNumber);
            var remoteAddr = HexToIp(parts[2].Split(':')[0]);
            var remotePort = int.Parse(parts[2].Split(':')[1], System.Globalization.NumberStyles.HexNumber);

            // Only show connections to non-localhost addresses
            if (!remoteAddr.StartsWith("127.") && remoteAddr != "0.0.0.0")
            {
                Console.WriteLine($"{protocol}: {localAddr}:{localPort} -> {remoteAddr}:{remotePort}");
                yield return new OpenedPoEConnection(localAddr, localPort, remoteAddr, remotePort);
            }
        }
    }

    static string HexToIp(string hex)
    {
        var bytes = new byte[4];
        for (var i = 0; i < 4; ++i)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        Array.Reverse(bytes);
        return string.Join(".", bytes);
    }

    private sealed record OpenedPoEConnection(string localAddr, int localPort, string remoteAddr, int remotePort);
}
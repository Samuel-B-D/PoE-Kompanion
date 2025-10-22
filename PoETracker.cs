namespace PoELogoutMacro;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

internal sealed class PoETracker
{
    public static readonly PoETracker Instance = new();

    public static void Initialize() { _ = Instance; }
    
    private Process? poeProcess;
    private List<OpenedPoEConnection> openedPoEConnections = [];
    
    public PoETracker()
    {
        SudoCheck();
            
        this.FindPoEExecutable();
        this.FindGameConnections();
        this.CloseGameConnections();
    }

    private static void SudoCheck()
    {
        if (Environment.UserName != "root")
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "notify-send",
                    Arguments = $"\"PoELogoutMacro\" \"PoELogoutMacro need to run as sudo to be allowed to close sockets\" --urgency=critical",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            proc.WaitForExit();
            
            Environment.Exit(1);
            return;
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
        }
        
        static void CloseGameConnection(IEnumerable<string> gameConnectionInodes)
        {
            foreach (var inode in gameConnectionInodes)
            {
                KillSocket(inode);
            }
    
            Console.WriteLine("Game connection closed!");
        }
        
        static void KillSocket(string inode)
        {
            try
            {
                // Try to find and close the socket by manipulating /proc/net/tcp
                // We'll use a raw socket approach to send RST packet
                var lines = File.ReadAllLines("/proc/net/tcp");
                
                foreach (var line in lines.Skip(1))
                {
                    var parts = line.Split([' '], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 10 || parts[9] != inode)
                        continue;
    
                    var localAddr = HexToIp(parts[1].Split(':')[0]);
                    var localPort = int.Parse(parts[1].Split(':')[1], System.Globalization.NumberStyles.HexNumber);
                    var remoteAddr = HexToIp(parts[2].Split(':')[0]);
                    var remotePort = int.Parse(parts[2].Split(':')[1], System.Globalization.NumberStyles.HexNumber);
                    
                    Console.WriteLine($"KILLING: {localAddr}:{localPort} -> {remoteAddr}:{remotePort}");
    
                    SendRstPacket(localAddr, localPort, remoteAddr, remotePort);
                    Console.WriteLine($"Sent RST to {remoteAddr}:{remotePort}");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error closing socket: {ex.Message}");
            }
        }
    
        static void SendRstPacket(OpenedPoEConnection openedPoEConnection) 
            => SendRstPacket(openedPoEConnection.localAddr, openedPoEConnection.localPort, openedPoEConnection.remoteAddr, openedPoEConnection.remotePort);
        static void SendRstPacket(string? localAddr, int? localPort, string remoteAddr, int remotePort)
        {
            try
            {
                // Use ss to kill the connection
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "bash",
                        Arguments = $"-c \"sudo ss -K dst {remoteAddr} dport {remotePort}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                proc.WaitForExit();
                
                Console.WriteLine($"Closed connection to {remoteAddr}:{remotePort}");
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
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
    
            return inodes;
        }
    
        static IEnumerable<OpenedPoEConnection> FindGameOpenedConnections(string path, HashSet<string> inodes, string protocol)
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
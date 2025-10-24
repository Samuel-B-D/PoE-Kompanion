namespace PoEKompanion;

using System;
using System.Diagnostics;
using System.Threading.Tasks;

public static class X11WindowManager
{
    public static bool TrySetAlwaysOnTop(int processId)
    {
        if (!OperatingSystem.IsLinux()) return false;

        return TryWithWmctrl(processId) || TryWithXdotool(processId);
    }

    private static bool TryWithWmctrl(int processId)
    {
        try
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = $"-c \"wmctrl -i -r $(wmctrl -lp | awk '$3 == {processId} {{print $1; exit}}') -b add,above\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            proc.Start();
            proc.WaitForExit(5000);

            return proc.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool TryWithXdotool(int processId)
    {
        try
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = $"-c \"xdotool search --pid {processId} windowraise %@ windowfocus %@ set_window --overrideredirect 1\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            proc.Start();
            proc.WaitForExit(5000);

            return proc.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

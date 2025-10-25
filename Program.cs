namespace PoEKompanion;

using System.Threading.Tasks;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;

internal static class Program
{
    private const int PR_SET_PDEATHSIG = 1;
    private const int SIGTERM = 15;

    [DllImport("libc", SetLastError = true)]
    private static extern int prctl(int option, int arg2, int arg3, int arg4, int arg5);

    private static string GetExecutablePath()
    {
        var appImagePath = Environment.GetEnvironmentVariable("APPIMAGE");
        if (!string.IsNullOrEmpty(appImagePath) && File.Exists(appImagePath)) return appImagePath;

        try
        {
            var selfExe = File.ResolveLinkTarget("/proc/self/exe", true);
            if (selfExe?.Exists == true) return selfExe.FullName;
        }
        catch (Exception) { /* nom */ }

        return Path.GetFullPath(Path.Join(AppDomain.CurrentDomain.BaseDirectory, AppDomain.CurrentDomain.FriendlyName));
    }

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static async Task Main(string[] args)
    {
        var path = GetExecutablePath();
        var currentProcess = Process.GetCurrentProcess();
        foreach (var process in Process.GetProcesses().Where(p => !p.Equals(currentProcess) && p.MainModule?.FileName == path))
        {
            Console.WriteLine("Killing previous orphaned process");
            try
            {
                process.Kill(true);
            } catch (Exception) { /* nom */ }
        }

        if (args.Length > 0 && args[0] == "--bg")
        {
            if (OperatingSystem.IsLinux())
            {
                prctl(PR_SET_PDEATHSIG, SIGTERM, 0, 0, 0);
            }

            var parentPid = args.Length > 1 && int.TryParse(args[1], out var pid) ? pid : -1;
            await PoETracker.Instance.RunAsync(parentPid);
        }
        else
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    private static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions
            {
                CompositionMode = [Win32CompositionMode.LowLatencyDxgiSwapChain, Win32CompositionMode.RedirectionSurface],
                RenderingMode = [Win32RenderingMode.AngleEgl, Win32RenderingMode.Software],
                ShouldRenderOnUIThread = false,
            })
            .With(new X11PlatformOptions
            {
                EnableIme = false,
                EnableMultiTouch = false,
                EnableSessionManagement = false,
                RenderingMode = [X11RenderingMode.Glx, X11RenderingMode.Software],
                ShouldRenderOnUIThread = true,
                UseRetainedFramebuffer = false,
            })
            .With(new SkiaOptions
            {
                UseOpacitySaveLayer = true,
            });
}
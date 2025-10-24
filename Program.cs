namespace PoEKompanion;

using System.Threading.Tasks;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia;

internal static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static async Task Main(string[] args)
    {
        var path = Path.GetFullPath(Path.Join(AppDomain.CurrentDomain.BaseDirectory, AppDomain.CurrentDomain.FriendlyName));
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
            await PoETracker.Instance.RunAsync();
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
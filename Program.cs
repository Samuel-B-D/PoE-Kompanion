using System.IO;
using System.Runtime.CompilerServices;

namespace PoELogoutMacro;

using System;

using Avalonia;

internal static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--bg")
        {
            PoETracker.InitializeBlocking();
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
                UseOpacitySaveLayer = false,
            })            
        ;
}
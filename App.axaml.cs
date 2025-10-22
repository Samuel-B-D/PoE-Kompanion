using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using SharpHook;
using SharpHook.Data;

namespace PoELogoutMacro;

using Avalonia;
using Avalonia.Markup.Xaml;

public class App : Application
{
    private const KeyCode HOTKEY = KeyCode.VcBackQuote;
    
    private Process? bgProcess = null;
    
    private readonly EventLoopGlobalHook hook = new();
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            string path = Path.GetFullPath(Path.Join(AppDomain.CurrentDomain.BaseDirectory, AppDomain.CurrentDomain.FriendlyName));
            Console.WriteLine("PATH: " + path);

            var args = desktop.Args;
            for (var i = 0; i < args?.Length; ++i)
            {
                if (args[i] == "--bg-path=" && i < args.Length - 2)
                {
                    path = args[i + 1].Trim();
                }
            }

            StartBackgroundProcess(path);
            this.InitHook();
        }

        base.OnFrameworkInitializationCompleted();
    }
    
    private void InitHook()
    {
        hook.KeyPressed += (sender, args) =>
        {
            if (args.Data.KeyCode == HOTKEY)
            {
                if (this.bgProcess is null) return;
                this.bgProcess.StandardInput.Write((char)DispatchedActions.ForceLogout);
                this.bgProcess.StandardInput.Flush();
            }
        };
        Task.Run(() => hook.RunAsync());
    }

    private void StartBackgroundProcess(string path)
    {
        this.bgProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                // FileName = path,
                FileName = "pkexec",
                Arguments = $"sudo {path} --bg",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = true,
            }
        };
        this.bgProcess.Start();
    }

    private void ExitAction(object? sender, EventArgs e)
    {
        this.bgProcess?.Close();
        Environment.Exit(0);
    }
}
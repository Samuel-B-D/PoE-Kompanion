namespace PoEKompanion;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using PoEKompanion.Views;
using SharpHook;
using SharpHook.Data;

using Avalonia;
using Avalonia.Markup.Xaml;

public class App : Application
{
    private KeyCode currentHotkey;

    private Process? bgProcess;

    private EventLoopGlobalHook? hook;

    public static App? Instance { get; private set; }
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Instance = this;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (this.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;
            desktop.MainWindow = null;

            var path = Path.GetFullPath(Path.Join(AppDomain.CurrentDomain.BaseDirectory, AppDomain.CurrentDomain.FriendlyName));

            var args = desktop.Args;
            for (var i = 0; i < args?.Length; ++i)
            {
                if (args[i] == "--bg-path=" && i < args.Length - 2)
                {
                    path = args[i + 1].Trim();
                }
            }

            _ = this.InitAsync(path);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task InitAsync(string backgroundProcessPath)
    {
        var config = await ConfigurationManager.LoadAsync();
        this.currentHotkey = config.LogoutHotkey;
        
        this.StartBackgroundProcess(backgroundProcessPath);
        this.InitHook();
    }
    
    private void InitHook()
    {
        this.hook = new EventLoopGlobalHook();
        this.hook.KeyPressed += (_, args) =>
        {
            if (args.Data.KeyCode != this.currentHotkey) return;
            if (this.bgProcess is null) return;

            this.bgProcess.StandardInput.Write((char)DispatchedActions.ForceLogout);
            this.bgProcess.StandardInput.Flush();
        };
        _ = Task.Run(() => this.hook.RunAsync());
    }

    public async Task SuspendMainHookAsync()
    {
        if (this.hook is null) return;

        var hookToDispose = this.hook;
        this.hook = null;
        await Task.Run(() => hookToDispose.Dispose());
    }

    public void ResumeMainHook()
    {
        if (this.hook is not null) return;
        InitHook();
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

    private async void ConfigureAction(object? sender, EventArgs e)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var configWindow = new ConfigurationWindow();
            configWindow.Closed += async (_, _) =>
            {
                var newConfig = await ConfigurationManager.LoadAsync();
                if (newConfig.LogoutHotkey != this.currentHotkey)
                {
                    this.currentHotkey = newConfig.LogoutHotkey;
                }
                ResumeMainHook();
            };
            configWindow.Show();
        });
    }

    private async Task UpdateHotkeyAsync(KeyCode newHotkey)
    {
        var oldHook = this.hook;

        this.currentHotkey = newHotkey;

        this.hook = new EventLoopGlobalHook();
        this.hook.KeyPressed += (_, args) =>
        {
            if (args.Data.KeyCode != this.currentHotkey) return;
            if (this.bgProcess is null) return;

            this.bgProcess.StandardInput.Write((char)DispatchedActions.ForceLogout);
            this.bgProcess.StandardInput.Flush();
        };

        _ = Task.Run(() => this.hook.RunAsync());

        if (oldHook is not null)
        {
            await Task.Run(() => oldHook.Dispose());
        }
    }

    private void ExitAction(object? sender, EventArgs e)
    {
        this.hook?.Dispose();
        this.hook = null;

        try
        {
            this.bgProcess?.Kill(true);
            this.bgProcess?.Close();
        } catch (Exception) { /* nom */ }

        Environment.Exit(0);
    }
}
namespace PoEKompanion;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

using SharpHook;
using SharpHook.Data;

using PoEKompanion.Views;

public class App : Application
{
    private ConfigurationModel? config;

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
#if DEBUG
        this.AttachDevTools();
#endif

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
        this.config = await ConfigurationManager.LoadAsync();
        
        this.StartBackgroundProcess(backgroundProcessPath);
        this.StartMainHook();
    }
    
    private void StartMainHook()
    {
        if (this.hook is not null) return;
        
        this.hook = new EventLoopGlobalHook();
        this.hook.KeyPressed += async (_, args) =>
        {
            if (args.Data.KeyCode == this.config?.LogoutHotkey)
            {
                if (this.bgProcess is null) return;

                await this.bgProcess.StandardInput.WriteAsync((char)DispatchedActions.ForceLogout);
                await this.bgProcess.StandardInput.FlushAsync();
            }
            else if (args.Data.KeyCode == this.config?.OpenSettingsHotkey)
            {
                await this.OpenConfiguration();
            }
        };
        _ = Task.Run(() => this.hook.RunAsync());
    }

    public void StopMainHook()
    {
        this.hook?.Dispose();
        this.hook = null;
    }

    private void StartBackgroundProcess(string path)
    {
        try
        {
            this.bgProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sudo",
                    Arguments = $"-n {path} --bg",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
            };
            this.bgProcess.Start();

            _ = Task.Run(() =>
            {
                this.bgProcess.WaitForExit();

                if (this.bgProcess.ExitCode != 0)
                {
                    try
                    {
                        this.bgProcess = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "pkexec",
                                Arguments = $"sudo {path} --bg",
                                UseShellExecute = false,
                                RedirectStandardInput = true,
                                RedirectStandardOutput = false,
                                RedirectStandardError = false,
                                CreateNoWindow = true,
                            },
                        };
                        this.bgProcess.Start();

                        this.bgProcess.WaitForExit();

                        if (this.bgProcess.ExitCode != 0)
                        {
                            Environment.Exit(1);
                        }
                    }
                    catch (Exception)
                    {
                        Environment.Exit(1);
                    }
                }
            });
        }
        catch (Exception)
        {
            try
            {
                this.bgProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "pkexec",
                        Arguments = $"sudo {path} --bg",
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = false,
                        RedirectStandardError = false,
                        CreateNoWindow = true,
                    },
                };
                this.bgProcess.Start();

                _ = Task.Run(() =>
                {
                    this.bgProcess.WaitForExit();

                    if (this.bgProcess.ExitCode != 0)
                    {
                        Environment.Exit(1);
                    }
                });
            }
            catch (Exception)
            {
                Environment.Exit(1);
            }
        }
    }

    private void ConfigureAction(object? sender, EventArgs e) => _ = this.OpenConfiguration();
    
    private async Task OpenConfiguration()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var configWindow = new ConfigurationWindow();
            configWindow.Closed += async (_, _) =>
            {
                this.config = await ConfigurationManager.LoadAsync();
                this.StartMainHook();
            };
            configWindow.Show();
        });
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
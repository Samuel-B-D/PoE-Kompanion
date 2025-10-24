namespace PoEKompanion;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

using SharpHook;

using Views;

public class App : Application
{
    private ConfigurationModel? config;

    private Process? bgProcess;

    private EventLoopGlobalHook? hook;

    private UnixSocketIpc? ipc;

    private bool isShuttingDown;

    private ConfigurationWindow? configWindow;

    private int? poeProcessId;

    public static App? Instance { get; private set; }

    public int? GetPoEProcessId() => this.poeProcessId;

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
        this.ipc = await UnixSocketIpc.CreateServerAsync();
        this.StartMainHook();
        _ = this.ListenForIpcMessagesAsync();
    }

    private async Task ListenForIpcMessagesAsync()
    {
        if (this.ipc is null) return;

        while (true)
        {
            var message = await this.ipc.ReceiveAsync();

            if (message is NotificationMessage notification)
            {
                if (notification.IsError)
                {
                    NotificationManager.SendError(notification.Title, notification.Message);
                }
                else
                {
                    NotificationManager.SendInfo(notification.Title, notification.Message);
                }
            }
            else if (message is SetAlwaysOnTopMessage setAlwaysOnTop)
            {
                this.poeProcessId = setAlwaysOnTop.ProcessId;
                _ = Task.Run(() =>
                {
                    if (WindowManager.TrySetAlwaysOnTop(setAlwaysOnTop.ProcessId))
                    {
                        Console.WriteLine("Set PoE window to always-on-top");
                    }
                });
            }
        }
    }
    
    private void StartMainHook()
    {
        if (this.hook is not null) return;
        
        this.hook = new EventLoopGlobalHook();
        this.hook.KeyPressed += async (_, args) =>
        {
            if (args.Data.KeyCode == this.config?.LogoutHotkey)
            {
                if (this.ipc is null) return;

                await this.ipc.SendAsync(new ForceLogoutMessage());
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
        var currentPid = Environment.ProcessId;

        try
        {
            this.bgProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sudo",
                    Arguments = $"-n {path} --bg {currentPid}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            this.bgProcess.Start();

            _ = Task.Run(() =>
            {
                this.bgProcess.WaitForExit();

                if (this.isShuttingDown) return;

                if (this.bgProcess.ExitCode != 0)
                {
                    try
                    {
                        this.bgProcess = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "pkexec",
                                Arguments = $"sudo {path} --bg {currentPid}",
                                UseShellExecute = false,
                                CreateNoWindow = true,
                            },
                        };
                        this.bgProcess.Start();

                        this.bgProcess.WaitForExit();

                        if (this.isShuttingDown) return;

                        if (this.bgProcess.ExitCode != 0)
                        {
                            NotifyInitializationError();
                        }
                        else
                        {
                            NotifyInitializationSuccess();
                        }
                    }
                    catch (Exception)
                    {
                        if (this.isShuttingDown) return;
                        NotifyInitializationError();
                    }
                }
                else
                {
                    NotifyInitializationSuccess();
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
                        Arguments = $"sudo {path} --bg {currentPid}",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    },
                };
                this.bgProcess.Start();

                _ = Task.Run(() =>
                {
                    this.bgProcess.WaitForExit();

                    if (this.isShuttingDown) return;

                    if (this.bgProcess.ExitCode != 0)
                    {
                        NotifyInitializationError();
                    }
                    else
                    {
                        NotifyInitializationSuccess();
                    }
                });
            }
            catch (Exception)
            {
                if (this.isShuttingDown) return;
                NotifyInitializationError();
            }
        }
    }

    private static void NotifyInitializationSuccess()
    {
        NotificationManager.SendInfo("initialized successfully!");
    }
    
    private static void NotifyInitializationError()
    {
        NotificationManager.SendError("Failed to initialize; application will exit");
        Environment.Exit(1);
    }

    private void ConfigureAction(object? sender, EventArgs e) => _ = this.OpenConfiguration();
    
    private async Task OpenConfiguration()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (this.configWindow is not null)
            {
                this.configWindow.Activate();
                ConfigurationWindow.EnsureAlwaysOnTop();
                return;
            }

            this.configWindow = new ConfigurationWindow();
            this.configWindow.Closed += async (_, _) =>
            {
                this.configWindow = null;
                this.config = await ConfigurationManager.LoadAsync();
                this.StartMainHook();
            };
            this.configWindow.Show();
        });
    }

    private void ExitAction(object? sender, EventArgs e)
    {
        this.isShuttingDown = true;

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
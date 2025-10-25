namespace PoEKompanion.Views;

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using SharpHook.Data;

public partial class ConfigurationWindow : Window, INotifyPropertyChanged
{
    public new event PropertyChangedEventHandler? PropertyChanged;

    private ConfigurationModel currentConfig;
    private InputBlockerOverlay? overlay;
    private CancellationTokenSource? pollCancellation;

    public KeyCode LogoutHotkey
    {
        get => this.currentConfig.LogoutHotkey;
        set
        {
            if (this.currentConfig.LogoutHotkey == value) return;
            this.currentConfig.LogoutHotkey = value;
            this.OnPropertyChanged();
        }
    }
    
    public KeyCode OpenSettingsHotkey
    {
        get => this.currentConfig.OpenSettingsHotkey;
        set
        {
            if (this.currentConfig.OpenSettingsHotkey == value) return;
            this.currentConfig.OpenSettingsHotkey = value;
            this.OnPropertyChanged();
        }
    }

    public ConfigurationWindow()
    {
        this.currentConfig = new ConfigurationModel();

        this.InitializeComponent();
        this.DataContext = this;

        _ = this.LoadConfiguration();

        this.Opened += (_, _) =>
        {
            App.Instance?.StopMainHook();

            this.Measure(Size.Infinity);
            this.PositionOverPoE();

            EnsureAlwaysOnTop();
            this.ShowOverlayIfGameDetected();
            this.StartPollingForGame();
        };

        this.Closing += (_, _) =>
        {
            this.pollCancellation?.Cancel();
            this.pollCancellation?.Dispose();
            this.overlay?.Close();
            this.overlay = null;
        };
    }

    private void PositionOverPoE()
    {
        var poeProcessId = App.Instance?.GetPoEProcessId();
        if (!poeProcessId.HasValue) return;

        var bounds = WindowManager.GetWindowBounds(poeProcessId.Value);
        if (!bounds.HasValue) return;

        var windowWidth = (int)this.DesiredSize.Width;
        var windowHeight = (int)this.DesiredSize.Height;

        var centerX = bounds.Value.X + (bounds.Value.Width - windowWidth) / 2;
        var centerY = bounds.Value.Y + (bounds.Value.Height - windowHeight) / 2;

        this.Position = new Avalonia.PixelPoint(centerX, centerY);
    }

    private void ShowOverlayIfGameDetected()
    {
        if (this.overlay is not null) return;

        var poeProcessId = App.Instance?.GetPoEProcessId();
        if (!poeProcessId.HasValue) return;

        this.overlay = new InputBlockerOverlay(this);
        this.overlay.PositionOverPoEWindow(poeProcessId.Value);
        this.overlay.Show();

        this.Activate();
    }

    private void StartPollingForGame()
    {
        this.pollCancellation = new CancellationTokenSource();
        var token = this.pollCancellation.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                if (this.overlay is not null) continue;

                var poeProcessId = App.Instance?.GetPoEProcessId();
                if (!poeProcessId.HasValue) continue;

                WindowManager.TrySetAlwaysOnTop(poeProcessId.Value);

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    this.ShowOverlayIfGameDetected();
                    this.PositionOverPoE();
                });

                EnsureAlwaysOnTop();
            }
        }, token);
    }

    public static void EnsureAlwaysOnTop()
    {
        _ = Task.Run(() =>
        {
            var currentPid = Environment.ProcessId;
            WindowManager.TryBringToFront(currentPid);
        });
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async Task LoadConfiguration()
    {
        this.currentConfig = await ConfigurationManager.LoadAsync();
        this.OnPropertyChanged(nameof(this.LogoutHotkey));
        this.OnPropertyChanged(nameof(this.OpenSettingsHotkey));
    }
    
    private async Task SaveConfiguration()
    {
        try
        {
            await ConfigurationManager.SaveAsync(this.currentConfig);
            this.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving configuration: {ex.Message}");
            NotificationManager.SendError("Error saving configuration");
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e) => _ = this.SaveConfiguration();

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

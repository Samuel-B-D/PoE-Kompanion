namespace PoEKompanion.Controls;

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SharpHook;
using SharpHook.Data;

public partial class HotkeyPickerButton : UserControl, INotifyPropertyChanged
{
    private KeyCode _selectedKeyCode = KeyCode.VcBackQuote;
    private bool _isListening;
    private SimpleGlobalHook? _captureHook;

    public new event PropertyChangedEventHandler? PropertyChanged;

    public KeyCode SelectedKeyCode
    {
        get => _selectedKeyCode;
        set
        {
            if (_selectedKeyCode == value) return;
            _selectedKeyCode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(KeyDisplayName));
        }
    }

    public bool IsListening
    {
        get => _isListening;
        private set
        {
            if (_isListening == value) return;
            _isListening = value;
            OnPropertyChanged();
        }
    }

    public string KeyDisplayName => FormatKeyName(_selectedKeyCode);

    public HotkeyPickerButton()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (IsListening) return;

        IsListening = true;
        await StartKeyCapture();
    }

    private async Task StartKeyCapture()
    {
        try
        {
            _captureHook = new SimpleGlobalHook();

            _captureHook.KeyPressed += async (_, args) =>
            {
                var keyCode = args.Data.KeyCode;

                if (keyCode == KeyCode.VcEscape)
                {
                    await Dispatcher.UIThread.InvokeAsync(async () => await StopKeyCaptureAsync());
                    return;
                }

                await Dispatcher.UIThread.InvokeAsync(() => SelectedKeyCode = keyCode);
                await Dispatcher.UIThread.InvokeAsync(async () => await StopKeyCaptureAsync());
            };

            await Task.Run(() => _captureHook.Run());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during key capture: {ex.Message}");
            await StopKeyCaptureAsync();
        }
    }

    private async Task StopKeyCaptureAsync()
    {
        if (_captureHook is null) return;

        var hookToDispose = _captureHook;
        _captureHook = null;

        await Task.Run(() => hookToDispose.Dispose());

        await Dispatcher.UIThread.InvokeAsync(() => IsListening = false);
    }

    private static string FormatKeyName(KeyCode keyCode)
    {
        var name = keyCode.ToString();
        if (name.StartsWith("Vc")) return name.Substring(2);
        return name;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

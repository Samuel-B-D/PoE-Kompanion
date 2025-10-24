namespace PoEKompanion.Controls;

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using SharpHook;
using SharpHook.Data;

public partial class HotkeyPickerButton : UserControl, INotifyPropertyChanged
{
    private KeyCode _selectedKeyCode = KeyCode.VcBackQuote;
    private bool _isListening;
    private EventLoopGlobalHook? _captureHook;

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
            _captureHook = new EventLoopGlobalHook();

            _captureHook.KeyPressed += (_, args) =>
            {
                var keyCode = args.Data.KeyCode;

                if (keyCode == KeyCode.VcEscape)
                {
                    StopKeyCapture();
                    return;
                }

                SelectedKeyCode = keyCode;
                StopKeyCapture();
            };

            await Task.Run(() => _captureHook.RunAsync());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during key capture: {ex.Message}");
            StopKeyCapture();
        }
    }

    private void StopKeyCapture()
    {
        if (_captureHook is null) return;

        _captureHook.Dispose();
        _captureHook = null;
        IsListening = false;
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

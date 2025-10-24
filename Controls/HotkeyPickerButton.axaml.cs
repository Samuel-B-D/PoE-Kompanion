namespace PoEKompanion.Controls;

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SharpHook;
using SharpHook.Data;

public partial class HotkeyPickerButton : UserControl, INotifyPropertyChanged
{
    private bool isListening;
    private SimpleGlobalHook? captureHook;

    public new event PropertyChangedEventHandler? PropertyChanged;

    public static readonly StyledProperty<KeyCode> SelectedKeyCodeProperty =
        AvaloniaProperty.Register<HotkeyPickerButton, KeyCode>(
            nameof(SelectedKeyCode),
            KeyCode.VcBackQuote,
            defaultBindingMode: BindingMode.TwoWay);

    public KeyCode SelectedKeyCode
    {
        get => this.GetValue(SelectedKeyCodeProperty);
        set => this.SetValue(SelectedKeyCodeProperty, value);
    }

    static HotkeyPickerButton()
    {
        SelectedKeyCodeProperty.Changed.AddClassHandler<HotkeyPickerButton>((control, args) =>
        {
            control.OnPropertyChanged(nameof(KeyDisplayName));
        });
    }

    public bool IsListening
    {
        get => this.isListening;
        private set
        {
            if (this.isListening == value) return;
            this.isListening = value;
            this.OnPropertyChanged();
            this.UpdateButtonStyle();
        }
    }

    private void UpdateButtonStyle()
    {
        if (this.pickerButton is null) return;

        if (this.isListening)
        {
            this.pickerButton.Classes.Add("listening");
        }
        else
        {
            this.pickerButton.Classes.Remove("listening");
        }
    }

    public string KeyDisplayName => FormatKeyName(this.SelectedKeyCode);

    public HotkeyPickerButton()
    {
        this.InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        this.pickerButton = this.FindControl<Button>("PickerButton") ?? throw new InvalidOperationException("PickerButton not found");
    }

    private Button? pickerButton;

    private void OnButtonClick(object? sender, RoutedEventArgs e) => _ = this.StartListening();

    private async Task StartListening()
    {
        if (this.IsListening) return;

        this.IsListening = true;
        await this.StartKeyCaptureAsync();
    }

    private async Task StartKeyCaptureAsync()
    {
        try
        {
            this.captureHook = new SimpleGlobalHook();

            this.captureHook.KeyPressed += async (_, args) =>
            {
                var keyCode = args.Data.KeyCode;

                if (keyCode == KeyCode.VcEscape)
                {
                    await Dispatcher.UIThread.InvokeAsync(async () => await this.StopKeyCaptureAsync());
                    return;
                }

                await Dispatcher.UIThread.InvokeAsync(() => this.SelectedKeyCode = keyCode);
                await Dispatcher.UIThread.InvokeAsync(async () => await this.StopKeyCaptureAsync());
            };

            await Task.Run(() => this.captureHook.Run());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during key capture: {ex.Message}");
            await this.StopKeyCaptureAsync();
        }
    }

    private async Task StopKeyCaptureAsync()
    {
        if (this.captureHook is null) return;

        var hookToDispose = this.captureHook;
        this.captureHook = null;

        await Task.Run(() => hookToDispose.Dispose());

        await Dispatcher.UIThread.InvokeAsync(() => this.IsListening = false);
    }

    private static string FormatKeyName(KeyCode keyCode)
    {
        var name = keyCode.ToString();
        return name.StartsWith("Vc") ? name[2..] : name;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

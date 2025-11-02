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
    private bool isCtrlPressed;
    private bool isShiftPressed;
    private bool isAltPressed;
    private bool isStopping;

    public new event PropertyChangedEventHandler? PropertyChanged;

    public static readonly StyledProperty<HotkeyCombo?> SelectedHotkeyProperty =
        AvaloniaProperty.Register<HotkeyPickerButton, HotkeyCombo?>(
            nameof(SelectedHotkey),
            new HotkeyCombo(KeyCode.VcBackQuote),
            defaultBindingMode: BindingMode.TwoWay);

    public HotkeyCombo? SelectedHotkey
    {
        get => this.GetValue(SelectedHotkeyProperty);
        set => this.SetValue(SelectedHotkeyProperty, value);
    }

    static HotkeyPickerButton()
    {
        SelectedHotkeyProperty.Changed.AddClassHandler<HotkeyPickerButton>((control, args) =>
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

    public string KeyDisplayName => this.SelectedHotkey?.ToString() ?? "None";

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
            this.isCtrlPressed = false;
            this.isShiftPressed = false;
            this.isAltPressed = false;
            this.isStopping = false;

            this.captureHook.KeyPressed += async (_, args) =>
            {
                if (this.isStopping) return;

                var keyCode = args.Data.KeyCode;

                if (IsModifierKey(keyCode))
                {
                    this.UpdateModifierState(keyCode, true);
                    return;
                }

                if (keyCode == KeyCode.VcEscape)
                {
                    await Dispatcher.UIThread.InvokeAsync(async () => await this.StopKeyCaptureAsync());
                    return;
                }

                var combo = new HotkeyCombo(
                    keyCode,
                    this.isCtrlPressed,
                    this.isShiftPressed,
                    this.isAltPressed
                );

                await Dispatcher.UIThread.InvokeAsync(() => this.SelectedHotkey = combo);
                await Dispatcher.UIThread.InvokeAsync(async () => await this.StopKeyCaptureAsync());
            };

            this.captureHook.KeyReleased += (_, args) =>
            {
                if (this.isStopping) return;

                var keyCode = args.Data.KeyCode;
                if (IsModifierKey(keyCode))
                {
                    this.UpdateModifierState(keyCode, false);
                }
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

        this.isStopping = true;

        var hookToDispose = this.captureHook;
        this.captureHook = null;

        await Task.Run(() => hookToDispose.Dispose());

        await Dispatcher.UIThread.InvokeAsync(() => this.IsListening = false);
    }

    private static bool IsModifierKey(KeyCode keyCode) =>
        keyCode is KeyCode.VcLeftControl or KeyCode.VcRightControl or
                   KeyCode.VcLeftShift or KeyCode.VcRightShift or
                   KeyCode.VcLeftAlt or KeyCode.VcRightAlt or
                   KeyCode.VcLeftMeta or KeyCode.VcRightMeta;

    private void UpdateModifierState(KeyCode keyCode, bool isPressed)
    {
        switch (keyCode)
        {
            case KeyCode.VcLeftControl:
            case KeyCode.VcRightControl:
                this.isCtrlPressed = isPressed;
                break;
            case KeyCode.VcLeftShift:
            case KeyCode.VcRightShift:
                this.isShiftPressed = isPressed;
                break;
            case KeyCode.VcLeftAlt:
            case KeyCode.VcRightAlt:
                this.isAltPressed = isPressed;
                break;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

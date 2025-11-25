namespace PoEKompanion.Views;

using System;
using Avalonia.Controls;
using Avalonia.Input;

public partial class InputBlockerOverlay : Window
{
    private readonly Window? parentWindow;

    public InputBlockerOverlay()
    {
        this.InitializeComponent();

        this.Opened += (_, _) =>
        {
            _ = System.Threading.Tasks.Task.Run(() =>
            {
                var currentPid = Environment.ProcessId;
                WindowManager.SetWindowSkipTaskbar(currentPid, "Input Blocker");
            });
        };
    }

    public InputBlockerOverlay(Window parentWindow) : this()
    {
        this.parentWindow = parentWindow;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        this.parentWindow?.Activate();
        e.Handled = true;
    }

    public void PositionOverPoEWindow(int poeProcessId)
    {
        var bounds = WindowManager.GetWindowBounds(poeProcessId);
        if (bounds is null) return;

        this.Position = new Avalonia.PixelPoint(bounds.Value.X, bounds.Value.Y);
        this.Width = bounds.Value.Width;
        this.Height = bounds.Value.Height;
    }
}

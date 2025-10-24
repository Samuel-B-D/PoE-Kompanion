namespace PoEKompanion.Views;

using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PoEKompanion.Controls;
using SharpHook.Data;

public partial class ConfigurationWindow : Window
{
    private readonly HotkeyPickerButton _logoutHotkeyPicker;
    private ConfigurationModel _currentConfig;

    public KeyCode SelectedLogoutHotkey => _logoutHotkeyPicker.SelectedKeyCode;

    public ConfigurationWindow()
    {
        InitializeComponent();

        _logoutHotkeyPicker = this.FindControl<HotkeyPickerButton>("LogoutHotkeyPicker")
                              ?? throw new InvalidOperationException("LogoutHotkeyPicker not found");

        _currentConfig = new ConfigurationModel();
        LoadConfiguration();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void LoadConfiguration()
    {
        _currentConfig = await ConfigurationManager.LoadAsync();
        _logoutHotkeyPicker.SelectedKeyCode = _currentConfig.LogoutHotkey;
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            _currentConfig.LogoutHotkey = _logoutHotkeyPicker.SelectedKeyCode;
            await ConfigurationManager.SaveAsync(_currentConfig);
            Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving configuration: {ex.Message}");
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}

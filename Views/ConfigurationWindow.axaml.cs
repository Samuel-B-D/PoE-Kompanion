namespace PoEKompanion.Views;

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using SharpHook.Data;

public partial class ConfigurationWindow : Window, INotifyPropertyChanged
{
    public new event PropertyChangedEventHandler? PropertyChanged;

    private ConfigurationModel currentConfig;

    private KeyCode logoutHotkey;
    public KeyCode LogoutHotkey
    {
        get => this.logoutHotkey;
        set
        {
            if (this.logoutHotkey == value) return;
            this.logoutHotkey = value;
            this.OnPropertyChanged();
        }
    }

    public ConfigurationWindow()
    {
        this.currentConfig = new ConfigurationModel();

        this.InitializeComponent();
        this.DataContext = this;

        _ = this.LoadConfiguration();

        this.Opened += (_, _) => App.Instance?.StopMainHook();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async Task LoadConfiguration()
    {
        this.currentConfig = await ConfigurationManager.LoadAsync();
        this.LogoutHotkey = this.currentConfig.LogoutHotkey;
    }
    
    private async Task SaveConfiguration()
    {
        try
        {
            this.currentConfig.LogoutHotkey = this.LogoutHotkey;
            await ConfigurationManager.SaveAsync(this.currentConfig);
            this.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving configuration: {ex.Message}");
            // TODO: Show user-friendly error dialog in Phase 5 with styled UI
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

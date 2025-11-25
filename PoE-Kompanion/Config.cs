namespace PoEKompanion;

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SharpHook.Data;

public class ConfigurationModel
{
    public HotkeyCombo LogoutHotkey { get; set; } = new(KeyCode.VcBackQuote);
    public HotkeyCombo OpenSettingsHotkey { get; set; } = new(KeyCode.VcF10);
    public HotkeyCombo HideoutHotkey { get; set; } = new(KeyCode.VcF5);
    public HotkeyCombo ExitHotkey { get; set; } = new(KeyCode.VcSpace, ctrl: true, shift: true);
}

[JsonSerializable(typeof(ConfigurationModel))]
[JsonSerializable(typeof(HotkeyCombo))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class ConfigJsonContext : JsonSerializerContext
{
}

public static class ConfigurationManager
{
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config",
        "poe-kompanion"
    );

    private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "config.json");

    public static async Task<ConfigurationModel> LoadAsync()
    {
        try
        {
            if (!File.Exists(ConfigFilePath)) return GetDefault();

            await using var stream = File.OpenRead(ConfigFilePath);
            var config = await JsonSerializer.DeserializeAsync(stream, ConfigJsonContext.Default.ConfigurationModel);

            if (config is null)
            {
                Console.WriteLine("Warning: Configuration file is empty or invalid. Using defaults.");
                return GetDefault();
            }

            if (!Enum.IsDefined(typeof(KeyCode), config.LogoutHotkey.Key))
            {
                Console.WriteLine("Warning: Invalid logout hotkey in configuration. Using default.");
                config.LogoutHotkey = new HotkeyCombo(KeyCode.VcBackQuote);
            }

            if (!Enum.IsDefined(typeof(KeyCode), config.OpenSettingsHotkey.Key))
            {
                Console.WriteLine("Warning: Invalid settings hotkey in configuration. Using default.");
                config.OpenSettingsHotkey = new HotkeyCombo(KeyCode.VcF10);
            }

            if (!Enum.IsDefined(typeof(KeyCode), config.HideoutHotkey.Key))
            {
                Console.WriteLine("Warning: Invalid hideout hotkey in configuration. Using default.");
                config.HideoutHotkey = new HotkeyCombo(KeyCode.VcF5);
            }

            if (!Enum.IsDefined(typeof(KeyCode), config.ExitHotkey.Key))
            {
                Console.WriteLine("Warning: Invalid exit hotkey in configuration. Using default.");
                config.ExitHotkey = new HotkeyCombo(KeyCode.VcSpace, ctrl: true, shift: true);
            }

            return config;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to load configuration: {ex.Message}. Using defaults.");
            return GetDefault();
        }
    }

    public static async Task SaveAsync(ConfigurationModel config)
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);

            var tempPath = ConfigFilePath + ".tmp";

            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, config, ConfigJsonContext.Default.ConfigurationModel);
            }

            File.Move(tempPath, ConfigFilePath, overwrite: true);

            Console.WriteLine($"Configuration saved to {ConfigFilePath}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save configuration: {ex.Message}", ex);
        }
    }

    private static ConfigurationModel GetDefault() => new();
}

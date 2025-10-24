namespace PoEKompanion;

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SharpHook.Data;

/// <summary>
/// Configuration data model for the application.
/// </summary>
public class ConfigurationModel
{
    /// <summary>
    /// The hotkey used to trigger the logout action.
    /// </summary>
    public KeyCode LogoutHotkey { get; set; } = KeyCode.VcBackQuote;
}

/// <summary>
/// JSON serialization context for AOT compatibility.
/// </summary>
[JsonSerializable(typeof(ConfigurationModel))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class ConfigJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Manages loading and saving application configuration.
/// </summary>
public static class ConfigurationManager
{
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config",
        "poe-kompanion"
    );

    private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "config.json");

    /// <summary>
    /// Loads configuration from disk. Returns default configuration if file doesn't exist or is invalid.
    /// </summary>
    public static ConfigurationModel Load()
    {
        try
        {
            // If config file doesn't exist, return defaults
            if (!File.Exists(ConfigFilePath))
            {
                return GetDefault();
            }

            // Read and deserialize configuration
            string json = File.ReadAllText(ConfigFilePath);
            var config = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.ConfigurationModel);

            // Validate configuration
            if (config == null)
            {
                Console.WriteLine("Warning: Configuration file is empty or invalid. Using defaults.");
                return GetDefault();
            }

            // Validate that the hotkey is a valid KeyCode
            if (!Enum.IsDefined(typeof(KeyCode), config.LogoutHotkey))
            {
                Console.WriteLine($"Warning: Invalid hotkey '{config.LogoutHotkey}' in configuration. Using default.");
                config.LogoutHotkey = KeyCode.VcBackQuote;
            }

            return config;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to load configuration: {ex.Message}. Using defaults.");
            return GetDefault();
        }
    }

    /// <summary>
    /// Saves configuration to disk. Creates directory if it doesn't exist.
    /// </summary>
    public static void Save(ConfigurationModel config)
    {
        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(ConfigDirectory);

            // Serialize to JSON
            string json = JsonSerializer.Serialize(config, ConfigJsonContext.Default.ConfigurationModel);

            // Write to temporary file first (atomic write)
            string tempPath = ConfigFilePath + ".tmp";
            File.WriteAllText(tempPath, json);

            // Move temporary file to actual config file (atomic operation on most systems)
            File.Move(tempPath, ConfigFilePath, overwrite: true);

            Console.WriteLine($"Configuration saved to {ConfigFilePath}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save configuration: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Returns the default configuration.
    /// </summary>
    public static ConfigurationModel GetDefault()
    {
        return new ConfigurationModel
        {
            LogoutHotkey = KeyCode.VcBackQuote
        };
    }
}

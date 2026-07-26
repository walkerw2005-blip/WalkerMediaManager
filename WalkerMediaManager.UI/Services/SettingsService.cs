using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WalkerMediaManager.UI.Services;

public static class SettingsService
{
    private static readonly object SyncRoot = new();

    public static string AppDataFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "AppData",
        "Local",
        "WalkerMediaManager");

    public static string SettingsPath => Path.Combine(AppDataFolder, "settings.json");

    public static string GetString(string key, string defaultValue = "")
    {
        lock (SyncRoot)
        {
            Dictionary<string, JsonElement> values = Load();
            return values.TryGetValue(key, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? defaultValue
                : defaultValue;
        }
    }

    public static int GetInt32(string key, int defaultValue = 0)
    {
        lock (SyncRoot)
        {
            Dictionary<string, JsonElement> values = Load();
            return values.TryGetValue(key, out JsonElement value) && value.TryGetInt32(out int result)
                ? result
                : defaultValue;
        }
    }

    public static void SetString(string key, string value) => SetValue(key, value ?? string.Empty);

    public static void SetInt32(string key, int value) => SetValue(key, value);

    private static void SetValue<T>(string key, T value)
    {
        lock (SyncRoot)
        {
            Directory.CreateDirectory(AppDataFolder);
            Dictionary<string, JsonElement> values = Load();
            values[key] = JsonSerializer.SerializeToElement(value);

            string temporaryPath = SettingsPath + ".tmp";
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(values, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, SettingsPath, true);
        }
    }

    private static Dictionary<string, JsonElement> Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            }

            string json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
                   ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception exception)
        {
            DiagnosticsService.LogException("SettingsService could not read settings.json. Defaults will be used.", exception);
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WalkerMediaManager.UI.Services;

public static class SettingsService
{
    private static readonly object SyncRoot = new();

    public static string AppDataFolder => ApplicationPaths.DataFolder;

    public static string SettingsPath => ApplicationPaths.SettingsPath;

    public static string GetString(string key, string defaultValue = "")
    {
        lock (SyncRoot)
        {
            Dictionary<string, SettingValue> values = Load();

            return values.TryGetValue(key, out SettingValue? value) &&
                   value.Kind == SettingValueKind.String
                ? value.StringValue ?? defaultValue
                : defaultValue;
        }
    }

    public static int GetInt32(string key, int defaultValue = 0)
    {
        lock (SyncRoot)
        {
            Dictionary<string, SettingValue> values = Load();

            return values.TryGetValue(key, out SettingValue? value) &&
                   value.Kind == SettingValueKind.Int32
                ? value.Int32Value
                : defaultValue;
        }
    }

    public static void SetString(string key, string value)
    {
        SetValue(
            key,
            SettingValue.FromString(value ?? string.Empty));
    }

    public static void SetInt32(string key, int value)
    {
        SetValue(
            key,
            SettingValue.FromInt32(value));
    }

    private static void SetValue(
        string key,
        SettingValue value)
    {
        lock (SyncRoot)
        {
            ApplicationPaths.EnsureDataFolderExists();

            Dictionary<string, SettingValue> values = Load();
            values[key] = value;

            string temporaryPath = SettingsPath + ".tmp";

            try
            {
                WriteSettingsFile(
                    temporaryPath,
                    values);

                File.Move(
                    temporaryPath,
                    SettingsPath,
                    true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    try
                    {
                        File.Delete(temporaryPath);
                    }
                    catch
                    {
                    }
                }
            }
        }
    }

    private static Dictionary<string, SettingValue> Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return NewDictionary();
            }

            string json = File.ReadAllText(SettingsPath);

            if (string.IsNullOrWhiteSpace(json))
            {
                return NewDictionary();
            }

            using JsonDocument document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return NewDictionary();
            }

            Dictionary<string, SettingValue> values = NewDictionary();

            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                switch (property.Value.ValueKind)
                {
                    case JsonValueKind.String:
                        values[property.Name] =
                            SettingValue.FromString(
                                property.Value.GetString() ?? string.Empty);
                        break;

                    case JsonValueKind.Number:
                        if (property.Value.TryGetInt32(out int intValue))
                        {
                            values[property.Name] =
                                SettingValue.FromInt32(intValue);
                        }
                        break;
                }
            }

            return values;
        }
        catch (Exception exception)
        {
            DiagnosticsService.LogException(
                "SettingsService could not read settings.json. Defaults will be used.",
                exception);

            return NewDictionary();
        }
    }

    private static void WriteSettingsFile(
        string path,
        Dictionary<string, SettingValue> values)
    {
        using FileStream stream = new(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);

        using Utf8JsonWriter writer = new(
            stream,
            new JsonWriterOptions
            {
                Indented = true
            });

        writer.WriteStartObject();

        foreach (KeyValuePair<string, SettingValue> item in values)
        {
            switch (item.Value.Kind)
            {
                case SettingValueKind.String:
                    writer.WriteString(
                        item.Key,
                        item.Value.StringValue ?? string.Empty);
                    break;

                case SettingValueKind.Int32:
                    writer.WriteNumber(
                        item.Key,
                        item.Value.Int32Value);
                    break;
            }
        }

        writer.WriteEndObject();
        writer.Flush();
    }

    private static Dictionary<string, SettingValue> NewDictionary()
    {
        return new Dictionary<string, SettingValue>(
            StringComparer.OrdinalIgnoreCase);
    }

    private enum SettingValueKind
    {
        String,
        Int32
    }

    private sealed class SettingValue
    {
        public SettingValueKind Kind { get; private init; }

        public string? StringValue { get; private init; }

        public int Int32Value { get; private init; }

        public static SettingValue FromString(string value)
        {
            return new SettingValue
            {
                Kind = SettingValueKind.String,
                StringValue = value
            };
        }

        public static SettingValue FromInt32(int value)
        {
            return new SettingValue
            {
                Kind = SettingValueKind.Int32,
                Int32Value = value
            };
        }
    }
}

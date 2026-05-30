using System;
using System.IO;
using System.Text.Json;

namespace AudioExtractor;

public sealed class AppSettings
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public string TargetFolderPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "AudioExtractor");
    public string Theme { get; set; } = "Dark";
    public string Bitrate { get; set; } = "256k";
    public bool IsMono { get; set; } = false;
    public bool Normalize { get; set; } = false;
    public string PostAction { get; set; } = "None";

    private static string SettingsDirectory
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "AudioExtractor");
        }
    }

    private static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(this, SerializerOptions);
        File.WriteAllText(SettingsPath, json);
    }
}

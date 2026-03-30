using System.IO;
using System.Text.Json;
using VoiceKeyboard.Models;

namespace VoiceKeyboard.Services;

public class SettingsService
{
    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VoiceKeyboard");

    private static readonly string SettingsPath = Path.Combine(AppDataDir, "settings.json");
    public static readonly string ModelsDir = Path.Combine(AppDataDir, "models");

    public AppSettings Settings { get; set; } = new();

    public void Load()
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            Directory.CreateDirectory(ModelsDir);

            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            Settings = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(Settings, options);
            File.WriteAllText(SettingsPath, json);
        }
        catch { /* silent fail */ }
    }
}

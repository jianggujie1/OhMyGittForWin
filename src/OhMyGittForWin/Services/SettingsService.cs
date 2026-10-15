using System;
using System.IO;
using System.Text.Json;
using System.Windows.Interop;
using System.Windows.Media;
using OhMyGittForWin.Models;

namespace OhMyGittForWin.Services;

public interface ISettingsService
{
    AppSettingsModel LoadSettings();
    void SaveSettings(AppSettingsModel settings);
    void ApplyRenderMode(bool enableGpu);
}

public class SettingsService : ISettingsService
{
    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OhMyGittForWin");

    private static string SettingsFilePath =>
        Path.Combine(SettingsDirectory, "app_settings.json");

    public AppSettingsModel LoadSettings()
    {
        try
        {
            string path = SettingsFilePath;
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<AppSettingsModel>(json);
                if (settings != null) return settings;
            }
        }
        catch
        {
            // Fallback to default values if file read/deserialize fails
        }

        return new AppSettingsModel();
    }

    public void SaveSettings(AppSettingsModel settings)
    {
        try
        {
            string dir = SettingsDirectory;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // Silently ignore or log if required
        }
    }

    public void ApplyRenderMode(bool enableGpu)
    {
        ApplyGlobalRenderMode(enableGpu);
    }

    public static void ApplyGlobalRenderMode(bool enableGpu)
    {
        try
        {
            RenderOptions.ProcessRenderMode = enableGpu ? RenderMode.Default : RenderMode.SoftwareOnly;
        }
        catch
        {
            // In case running in an environment where ProcessRenderMode cannot be modified
        }
    }
}

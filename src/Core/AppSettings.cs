using System.IO;
using System.Text.Json;

namespace ObbyistMacro.Core;

/// <summary>Persisted application settings (stored in %LOCALAPPDATA%\ObbyistMacro\settings.json).</summary>
public class AppSettings
{
    public double RobloxSensitivity { get; set; }
    public int RobloxFps { get; set; } = 60;
    public bool StartMinimized { get; set; }
    public FpsMacroSettings Fps { get; set; } = new();
    public WallhopSettings Wallhop { get; set; } = new();
    public FreezeSettings Freeze { get; set; } = new();

    public class FpsMacroSettings
    {
        public bool Enabled { get; set; }
        public string Key { get; set; }
        public int FpsDown { get; set; }
        public int UpCount { get; set; }
        public int DownCount { get; set; }
        public string CurrentCap { get; set; } = "60";
    }

    public class WallhopSettings
    {
        public bool Enabled { get; set; }
        public string Key { get; set; }
    }

    public class FreezeSettings
    {
        public bool Enabled { get; set; }
        public string Key { get; set; }
        public string Mode { get; set; } = "Toggle";
    }
}

public static class SettingsService
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ObbyistMacro");
    private static readonly string File = Path.Combine(Dir, "settings.json");

    private static readonly object Lock = new();
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            lock (Lock)
            {
                if (System.IO.File.Exists(File))
                {
                    string json = System.IO.File.ReadAllText(File);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
        }
        catch { }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(Dir);
                string json = JsonSerializer.Serialize(settings, JsonOpts);
                string tmp = File + ".tmp";
                System.IO.File.WriteAllText(tmp, json);
                System.IO.File.Move(tmp, File, true);
            }
        }
        catch { }
    }
}
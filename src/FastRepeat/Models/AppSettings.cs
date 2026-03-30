using System.Text.Json;

namespace FastRepeat.Models;

public class AppSettings
{
    public int           RepeatIntervalMs { get; set; } = 100;
    public bool          IsSpeedLocked    { get; set; } = false;
    public bool          IsEnabled        { get; set; } = true;
    public List<KeyBinding> Bindings      { get; set; } = [];

    // ── Persistence ──────────────────────────────────────────────────────────

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FastRepeat", "settings.json");

    private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var text = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(text, _jsonOpts) ?? new();
            }
        }
        catch { /* corrupt file – start fresh */ }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, _jsonOpts));
        }
        catch { }
    }
}

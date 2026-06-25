using EwrcScraper.Models;
using Newtonsoft.Json;

namespace EwrcScraper.Services;

public class PreferencesService
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EwrcScraper", "preferences.json");

    public AppPreferences Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonConvert.DeserializeObject<AppPreferences>(json) ?? new AppPreferences();
            }
        }
        catch { }
        return new AppPreferences();
    }

    public void Save(AppPreferences prefs)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(prefs, Formatting.Indented));
        }
        catch { }
    }
}

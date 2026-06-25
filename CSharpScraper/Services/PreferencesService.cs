using EwrcScraper.Models;
using Newtonsoft.Json;

namespace EwrcScraper.Services;

public class PreferencesService
{
    public static readonly string ConfigPath = Path.Combine(
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

    // Updates only the ledenlijst path without touching any other preference
    public void SaveLedenlijstPad(string pad)
    {
        var prefs = Load();
        prefs.LedenlijstPad = pad;
        Save(prefs);
    }

    // Updates only the selected country IDs without touching any other preference
    public void SaveCountryIds(List<int> ids)
    {
        var prefs = Load();
        prefs.GeselecteerdeCountryIds = ids;
        Save(prefs);
    }
}

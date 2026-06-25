using System.Net.Http;
using EwrcScraper.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EwrcScraper.Services;

public class EwrcApiService
{
    private readonly HttpClient _http;
    private readonly DebugService _debug;
    private const string ApiBase = "https://api-next.ewrc-results.com";

    public EwrcApiService(HttpClient http, DebugService debug)
    {
        _http = http;
        _debug = debug;
    }

    public async Task<List<Country>> GetCountriesAsync(int year)
    {
        _debug.Log($"Landen ophalen voor jaar {year}...");
        var url = $"{ApiBase}/calendar/{year}/natall";
        try
        {
            var json = await _http.GetStringAsync(url);
            _debug.Log($"Landen API antwoord (eerste 200 tekens): {json[..Math.Min(200, json.Length)]}");
            var array = ParseToArray(json);
            var result = new List<Country>();
            foreach (var item in array)
            {
                result.Add(new Country
                {
                    Id = item["id"]?.Value<int>() ?? 0,
                    Name = ExtractName(item["name"]),
                    Code = item["shortcut"]?.Value<string>() ?? item["code"]?.Value<string>() ?? string.Empty,
                    Flag = item["flag"]?.Value<string>() ?? string.Empty,
                });
            }
            _debug.Log($"{result.Count} landen geladen.");
            return result;
        }
        catch (Exception ex)
        {
            _debug.Log($"Fout bij ophalen landen: {ex.Message}");
            throw new Exception($"Kan landen niet ophalen van eWRC: {ex.Message}", ex);
        }
    }

    public async Task<List<RallyEvent>> GetCalendarAsync(int year, IEnumerable<int> countryIds)
    {
        var idList = countryIds.ToList();
        _debug.Log($"Kalender ophalen: jaar={year}, {idList.Count} landen: [{string.Join(",", idList)}]");

        var result = new List<RallyEvent>();
        var seen = new HashSet<int>();

        foreach (var countryId in idList)
        {
            var url = $"{ApiBase}/calendar/{year}/list?nat={countryId}";
            _debug.Log($"  Ophalen land {countryId}: {url}");
            try
            {
                var json = await _http.GetStringAsync(url);
                var weeks = ParseToArray(json);

                // Response is grouped by week: [{week:1, events:[...]}, {week:2, events:[...]}, ...]
                foreach (var week in weeks)
                {
                    var events = week["events"] as JArray ?? new JArray();
                    foreach (var item in events)
                    {
                        var id = item["id"]?.Value<int>() ?? 0;
                        if (id == 0 || !seen.Add(id)) continue;
                        result.Add(new RallyEvent
                        {
                            Id = id,
                            Name = ExtractName(item["name"]),
                            Season = item["season"]?.Value<int>() ?? year,
                            From = item["from"]?.Value<string>() ?? string.Empty,
                            Until = item["until"]?.Value<string>() ?? string.Empty,
                            Days = item["days"]?.Value<int>() ?? 0,
                            Country = item["country"]?.Value<string>() ?? string.Empty,
                            Flag = item["flag"]?.Value<string>() ?? string.Empty,
                            Slug = item["slug"]?.Value<string>() ?? string.Empty,
                            Cancelled = item["cancelled"]?.Value<int>() ?? 0,
                            Url = $"https://www.ewrc-results.com/event/{id}-{item["slug"]?.Value<string>()}/",
                        });
                    }
                }
                _debug.Log($"  Land {countryId}: {seen.Count} unieke rally's tot nu toe.");
            }
            catch (Exception ex)
            {
                _debug.Log($"  Fout land {countryId}: {ex.Message}");
            }
        }

        _debug.Log($"{result.Count} rally's geladen voor {idList.Count} landen.");
        return result;
    }

    public async Task<List<DriverEntry>> GetEntriesAsync(int rallyId, string rallyName)
    {
        _debug.Log($"Inschrijvingen ophalen voor {rallyName} (id={rallyId})...");
        var urls = new[]
        {
            $"{ApiBase}/event/{rallyId}/entries",
            $"{ApiBase}/entries/{rallyId}"
        };

        string? json = null;
        foreach (var url in urls)
        {
            try
            {
                _debug.Log($"  Probeer: {url}");
                json = await _http.GetStringAsync(url);
                break;
            }
            catch (Exception ex)
            {
                _debug.Log($"  Mislukt ({url}): {ex.Message}");
            }
        }

        if (json == null)
        {
            _debug.Log($"  Beide endpoints mislukt voor {rallyName}.");
            return new List<DriverEntry>();
        }

        _debug.Log($"  Antwoord (eerste 200): {json[..Math.Min(200, json.Length)]}");

        try
        {
            var entries = new List<DriverEntry>();
            var token = JToken.Parse(json);

            // Determine where the entry list lives
            JArray entryArray;
            if (token is JArray arr)
            {
                entryArray = arr;
            }
            else if (token is JObject obj && obj["entries"] is JArray ea)
            {
                entryArray = ea;
            }
            else if (token is JObject obj2)
            {
                // Fallback: top-level drivers / codrivers arrays
                if (obj2["drivers"] is JArray drivers)
                    foreach (var drv in drivers)
                        AddEntry(entries, drv, "Driver", rallyName, rallyId);

                if (obj2["codrivers"] is JArray codrivers)
                    foreach (var codrv in codrivers)
                        AddEntry(entries, codrv, "CoPilot", rallyName, rallyId);

                _debug.Log($"  {entries.Count} inschrijvingen (top-level arrays) voor {rallyName}.");
                return entries;
            }
            else
            {
                _debug.Log($"  Onbekende structuur voor {rallyName}.");
                return entries;
            }

            foreach (var item in entryArray)
            {
                // driver can be under "driver" or "pilot"
                var drv = item["driver"] ?? item["pilot"];
                if (drv != null) AddEntry(entries, drv, "Driver", rallyName, rallyId);

                // codriver can be under "codriver" or "copilot"
                var codrv = item["codriver"] ?? item["copilot"];
                if (codrv != null) AddEntry(entries, codrv, "CoPilot", rallyName, rallyId);
            }

            _debug.Log($"  {entries.Count} inschrijvingen geladen voor {rallyName}.");
            return entries;
        }
        catch (Exception ex)
        {
            _debug.Log($"  Parseerfout voor {rallyName}: {ex.Message}");
            return new List<DriverEntry>();
        }
    }

    private static void AddEntry(List<DriverEntry> list, JToken person, string type, string rallyName, int rallyId)
    {
        var firstName = person["firstname"]?.Value<string>() ?? string.Empty;
        var lastName  = person["lastname"]?.Value<string>() ?? string.Empty;
        var name = $"{firstName} {lastName}".Trim();
        if (string.IsNullOrEmpty(name)) name = ExtractName(person["name"]);
        if (string.IsNullOrEmpty(name)) return;

        var number = person["id"]?.Value<int>().ToString() ?? string.Empty;
        list.Add(new DriverEntry { Name = name, Number = number, Type = type, RallyName = rallyName, RallyId = rallyId });
    }

    public async Task<List<SearchResult>> SearchAsync(string query)
    {
        _debug.Log($"Zoeken naar: {query}");
        var url = $"{ApiBase}/search?query={Uri.EscapeDataString(query)}&limit=10";
        try
        {
            var json = await _http.GetStringAsync(url);
            var array = JArray.Parse(json);
            var results = new List<SearchResult>();
            foreach (var item in array)
            {
                results.Add(new SearchResult
                {
                    Id = item["id"]?.Value<int>().ToString() ?? string.Empty,
                    Name = item["name"]?.Value<string>() ?? string.Empty,
                    Type = item["type"]?.Value<string>() ?? string.Empty,
                    Country = item["country"]?.Value<string>() ?? string.Empty,
                    Extra = item["extra"]?.Value<string>() ?? string.Empty,
                });
            }
            _debug.Log($"{results.Count} zoekresultaten.");
            return results;
        }
        catch (Exception ex)
        {
            _debug.Log($"Zoekfout: {ex.Message}");
            throw new Exception($"Zoeken mislukt: {ex.Message}", ex);
        }
    }

    public async Task<DriverProfile> GetDriverProfileAsync(string driverId)
    {
        _debug.Log($"Rijdersprofiel ophalen: id={driverId}");
        var profileUrl = $"{ApiBase}/driver/{driverId}";
        var statsUrl = $"{ApiBase}/driver/{driverId}/categories?all=true";

        try
        {
            var profileTask = _http.GetStringAsync(profileUrl);
            var statsTask = _http.GetStringAsync(statsUrl);
            await Task.WhenAll(profileTask, statsTask);

            var profileJson = JObject.Parse(await profileTask);
            var statsArray = JArray.Parse(await statsTask);

            var profile = new DriverProfile
            {
                Id = driverId,
                Name = profileJson["name"]?.Value<string>() ?? string.Empty,
                Type = profileJson["type"]?.Value<string>() ?? string.Empty,
                Country = profileJson["country"]?.Value<string>() ?? string.Empty,
                Born = profileJson["born"]?.Value<string>() ?? string.Empty,
                PhotoUrl = profileJson["photo"]?.Value<string>() ?? string.Empty,
            };

            foreach (var stat in statsArray)
            {
                profile.Stats.Add(new DriverCategoryStats
                {
                    Category = stat["category"]?.Value<string>() ?? string.Empty,
                    Starts = stat["starts"]?.Value<int>() ?? 0,
                    Wins = stat["wins"]?.Value<int>() ?? 0,
                    Podiums = stat["podiums"]?.Value<int>() ?? 0,
                    Finishes = stat["finishes"]?.Value<int>() ?? 0,
                });
            }

            return profile;
        }
        catch (Exception ex)
        {
            _debug.Log($"Fout profiel ophalen: {ex.Message}");
            throw new Exception($"Kan rijdersprofiel niet laden: {ex.Message}", ex);
        }
    }

    public async Task<byte[]?> DownloadImageAsync(string url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        try
        {
            return await _http.GetByteArrayAsync(url);
        }
        catch
        {
            return null;
        }
    }

    // Parses a JSON response that may be either an array [...] or an object {...}
    private static JArray ParseToArray(string json)
    {
        var trimmed = json.TrimStart();
        if (trimmed.StartsWith('['))
            return JArray.Parse(json);
        var obj = JObject.Parse(json);
        var arr = new JArray();
        foreach (var val in obj.PropertyValues())
            arr.Add(val);
        return arr;
    }

    // Extracts a display name from a field that is either a plain string or a {"nl":"...","en":"..."} object
    private static string ExtractName(JToken? token)
    {
        if (token == null) return string.Empty;
        if (token.Type == JTokenType.Object)
        {
            var obj = (JObject)token;
            return obj["nl"]?.Value<string>()
                ?? obj["en"]?.Value<string>()
                ?? obj.Properties().FirstOrDefault()?.Value.Value<string>()
                ?? string.Empty;
        }
        return token.Value<string>() ?? string.Empty;
    }
}

using System.Net.Http;
using EwrcScraper.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EwrcScraper.Services;

public class EwrcApiService
{
    private readonly HttpClient _http;
    private readonly DebugService _debug;
    private readonly string _apiBase;

    public EwrcApiService(HttpClient http, DebugService debug, string apiBase = "https://api-next.ewrc-results.com")
    {
        _http = http;
        _debug = debug;
        _apiBase = apiBase;
    }

    public async Task<List<Country>> GetCountriesAsync(int year)
    {
        _debug.Log($"Landen ophalen voor jaar {year}...");
        var url = $"{_apiBase}/calendar/{year}/natall";
        _debug.Log($"GET {url}");
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
            var url = $"{_apiBase}/calendar/{year}/list?nat={countryId}";
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
            $"{_apiBase}/event/{rallyId}/entries",
            $"{_apiBase}/entries/{rallyId}"
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
        var url = $"{_apiBase}/search?query={Uri.EscapeDataString(query)}&limit=10";
        _debug.Log($"GET {url}");
        try
        {
            var json = await _http.GetStringAsync(url);
            _debug.Log($"  Antwoord (eerste 300): {json[..Math.Min(300, json.Length)]}");
            var array = ExtractSearchArray(json);
            var results = new List<SearchResult>();
            foreach (var item in array)
            {
                if (item.Type != JTokenType.Object) continue;

                // Persons use firstname/lastname; events and others may use a plain "name".
                var first = item["firstname"]?.Value<string>() ?? string.Empty;
                var last = item["lastname"]?.Value<string>() ?? string.Empty;
                var naam = $"{first} {last}".Trim();
                if (string.IsNullOrEmpty(naam)) naam = ExtractName(item["name"]);

                var vlag = item["flag"]?.Value<string>()
                    ?? item["country"]?.Value<string>()
                    ?? item["nationality"]?.Value<string>()
                    ?? string.Empty;

                results.Add(new SearchResult
                {
                    Id = item["id"]?.ToString() ?? string.Empty,
                    Name = naam,
                    Type = item["type"]?.Value<string>() ?? item["__group"]?.Value<string>() ?? string.Empty,
                    Country = vlag.ToUpperInvariant(),
                    Extra = item["slug"]?.Value<string>() ?? item["extra"]?.Value<string>() ?? string.Empty,
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

    public async Task<DriverProfile> GetDriverProfileAsync(string driverId, string type = "driver")
    {
        // Drivers and codrivers share numeric IDs but live in separate API namespaces.
        var endpoint = type.Equals("codriver", StringComparison.OrdinalIgnoreCase) ? "codriver" : "driver";
        _debug.Log($"Rijdersprofiel ophalen: id={driverId}, type={endpoint}");
        var profileUrl = $"{_apiBase}/{endpoint}/{driverId}";
        var statsUrl = $"{_apiBase}/{endpoint}/{driverId}/categories?all=true";
        _debug.Log($"GET {profileUrl}");
        _debug.Log($"GET {statsUrl}");

        try
        {
            // Fetch both independently: the profile endpoint can 404 while the
            // categories endpoint still works (and vice versa). Don't let one
            // failure wipe out the other.
            var profileJson = await TryGetJObjectAsync(profileUrl);
            var statsToken = await TryGetJTokenAsync(statsUrl);

            if (profileJson == null && statsToken == null)
                throw new Exception("Zowel profiel- als statistiek-endpoint gaven geen gegevens (404).");

            // API returns firstname+lastname, not a combined "name"
            var firstName = profileJson?["firstname"]?.Value<string>() ?? string.Empty;
            var lastName  = profileJson?["lastname"]?.Value<string>() ?? string.Empty;
            var fullName  = $"{firstName} {lastName}".Trim();
            if (string.IsNullOrEmpty(fullName))
                fullName = profileJson?["name"]?.Value<string>() ?? string.Empty;

            // Country is under nation.name (multilingual) with a flag code in nation.flag
            var nation = profileJson?["nation"] as JObject;
            var countryName = string.Empty;
            if (nation?["name"] is JObject nameObj)
                countryName = nameObj["nl"]?.Value<string>()
                    ?? nameObj["en"]?.Value<string>()
                    ?? string.Empty;
            if (string.IsNullOrEmpty(countryName))
                countryName = (nation?["flag"]?.Value<string>() ?? string.Empty).ToUpperInvariant();

            var slug = profileJson?["slug"]?.Value<string>() ?? string.Empty;
            var rawPhoto = profileJson?["photo"]?.Value<string>() ?? string.Empty;
            // Photos are served from the media host, not www.
            var photoUrl = string.IsNullOrEmpty(rawPhoto)
                ? string.Empty
                : $"https://media.ewrc-results.com/photos/{rawPhoto}";
            var pageSegment = endpoint == "codriver" ? "coprofile" : "profile";
            var profilePageUrl = string.IsNullOrEmpty(slug)
                ? string.Empty
                : $"https://www.ewrc-results.com/{pageSegment}/{driverId}-{slug}/";

            var profile = new DriverProfile
            {
                Id = driverId,
                Name = fullName,
                Type = profileJson?["type"]?.Value<string>() ?? string.Empty,
                Country = countryName,
                Born = profileJson?["born"]?.Value<string>() ?? string.Empty,
                PhotoUrl = photoUrl,
                ProfileUrl = profilePageUrl,
            };

            // The categories endpoint may return a flat array or
            // {sections:[],categories:[{key,raceType,stats:[{key,value}]}]}
            JArray categoriesArray;
            if (statsToken is JArray directArr)
                categoriesArray = directArr;
            else if (statsToken is JObject statsObj && statsObj["categories"] is JArray cats)
                categoriesArray = cats;
            else
                categoriesArray = new JArray();

            foreach (var cat in categoriesArray)
            {
                var catStats = cat["stats"] as JArray;
                int GetStat(string key) => catStats?
                    .FirstOrDefault(s => s["key"]?.Value<string>() == key)?
                    ["value"]?.Value<int>() ?? 0;
                double GetPct(string key) => catStats?
                    .FirstOrDefault(s => s["key"]?.Value<string>() == key)?
                    ["percentage"]?.Value<double>() ?? 0;

                var raceType = cat["raceType"]?.Value<string>()
                    ?? cat["key"]?.Value<string>()
                    ?? cat["category"]?.Value<string>()
                    ?? "?";

                // Fallback for older flat-array shape where stats are direct properties
                if (catStats == null)
                {
                    var u = cat["retirements"]?.Value<int>() ?? 0;
                    var st = cat["starts"]?.Value<int>() ?? 0;
                    profile.Stats.Add(new DriverCategoryStats
                    {
                        Category = raceType,
                        Starts = st,
                        Wins = cat["wins"]?.Value<int>() ?? 0,
                        KlasseWinst = cat["class_wins"]?.Value<int>() ?? 0,
                        Uitval = u,
                        UitvalPct = st > 0 ? Math.Round(100.0 * u / st, 1) : 0,
                    });
                }
                else
                {
                    profile.Stats.Add(new DriverCategoryStats
                    {
                        Category = raceType.Length > 0
                            ? char.ToUpper(raceType[0]) + raceType[1..]
                            : raceType,
                        Starts = GetStat("lg_starts"),
                        Wins = GetStat("lg_winsU"),
                        KlasseWinst = GetStat("lg_class_wins"),
                        Uitval = GetStat("lg_countretirement"),
                        UitvalPct = GetPct("lg_countretirement"),
                    });
                }
            }

            return profile;
        }
        catch (Exception ex)
        {
            _debug.Log($"Fout profiel ophalen: {ex.Message}");
            throw new Exception($"Kan rijdersprofiel niet laden: {ex.Message}", ex);
        }
    }

    // GETs a URL and parses the body as a JObject; returns null on 404 / parse failure.
    private async Task<JObject?> TryGetJObjectAsync(string url)
    {
        if (await TryGetJTokenAsync(url) is JObject obj) return obj;
        return null;
    }

    // GETs a URL and parses the body as a JToken; returns null on a non-success
    // status (e.g. 404) so a missing endpoint doesn't abort the whole load.
    private async Task<JToken?> TryGetJTokenAsync(string url)
    {
        try
        {
            using var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _debug.Log($"  {(int)response.StatusCode} bij {url}");
                return null;
            }
            var json = await response.Content.ReadAsStringAsync();
            return JToken.Parse(json);
        }
        catch (Exception ex)
        {
            _debug.Log($"  Fout bij {url}: {ex.Message}");
            return null;
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

    // Extracts the list of search hits from a /search response that may be:
    //   - a top-level array:            [ {..}, {..} ]
    //   - an object with a list key:    { "results": [..] }
    //   - groups of arrays:             { "drivers": [..], "events": [..] }
    //   - groups of {total,items}:      { "drivers": {"total":9,"items":[..]}, "codrivers": {..} }   (current eWRC shape)
    // For the grouped cases the group key is injected as "__group" so each hit keeps its type.
    private static JArray ExtractSearchArray(string json)
    {
        var token = JToken.Parse(json);
        if (token is JArray arr) return arr;
        if (token is not JObject obj) return new JArray();

        // Single wrapper array under a common key
        foreach (var key in new[] { "results", "data", "items", "hits" })
            if (obj[key] is JArray wrapped) return wrapped;

        // Gather hits from each top-level group, whether it is an array or a {total, items} object
        var combined = new JArray();
        foreach (var prop in obj.Properties())
        {
            var groupArr = prop.Value switch
            {
                JArray a => a,
                JObject go when go["items"] is JArray ia => ia,
                _ => null
            };
            if (groupArr == null) continue;

            foreach (var item in groupArr)
            {
                if (item is JObject itemObj && itemObj["type"] == null)
                    itemObj["__group"] = prop.Name;
                combined.Add(item);
            }
        }
        return combined;
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

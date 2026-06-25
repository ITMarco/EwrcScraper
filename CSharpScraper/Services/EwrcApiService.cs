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
            var array = JArray.Parse(json);
            var result = new List<Country>();
            foreach (var item in array)
            {
                result.Add(new Country
                {
                    Id = item["id"]?.Value<int>() ?? 0,
                    Name = item["name"]?.Value<string>() ?? string.Empty,
                    Code = item["code"]?.Value<string>() ?? string.Empty,
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
        var natParam = string.Join(",", countryIds);
        _debug.Log($"Kalender ophalen: jaar={year}, landen={natParam}");
        var url = $"{ApiBase}/calendar/{year}/list?nat={natParam}";
        try
        {
            var json = await _http.GetStringAsync(url);
            var array = JArray.Parse(json);
            var result = new List<RallyEvent>();
            foreach (var item in array)
            {
                result.Add(new RallyEvent
                {
                    Id = item["id"]?.Value<int>() ?? 0,
                    Name = item["name"]?.Value<string>() ?? string.Empty,
                    Season = item["season"]?.Value<int>() ?? year,
                    From = item["from"]?.Value<string>() ?? string.Empty,
                    Until = item["until"]?.Value<string>() ?? string.Empty,
                    Days = item["days"]?.Value<int>() ?? 0,
                    Country = item["country"]?.Value<string>() ?? string.Empty,
                    Flag = item["flag"]?.Value<string>() ?? string.Empty,
                    Slug = item["slug"]?.Value<string>() ?? string.Empty,
                    Cancelled = item["cancelled"]?.Value<int>() ?? 0,
                    Url = $"https://www.ewrc-results.com/event/{item["id"]?.Value<int>()}-{item["slug"]?.Value<string>()}/",
                });
            }
            _debug.Log($"{result.Count} rally's geladen.");
            return result;
        }
        catch (Exception ex)
        {
            _debug.Log($"Fout bij ophalen kalender: {ex.Message}");
            throw new Exception($"Kan kalender niet ophalen van eWRC: {ex.Message}", ex);
        }
    }

    public async Task<List<DriverEntry>> GetEntriesAsync(int rallyId, string rallyName)
    {
        _debug.Log($"Inschrijvingen ophalen voor {rallyName} (id={rallyId})...");
        var url = $"{ApiBase}/event/{rallyId}/entries";
        try
        {
            var json = await _http.GetStringAsync(url);
            var array = JArray.Parse(json);
            var entries = new List<DriverEntry>();

            foreach (var item in array)
            {
                var driverName = item["driver"]?["name"]?.Value<string>();
                var driverNum = item["driver"]?["id"]?.Value<int>().ToString();
                var coName = item["codriver"]?["name"]?.Value<string>();
                var coNum = item["codriver"]?["id"]?.Value<int>().ToString();

                if (!string.IsNullOrEmpty(driverName))
                    entries.Add(new DriverEntry { Name = driverName, Number = driverNum ?? string.Empty, Type = "Driver", RallyName = rallyName, RallyId = rallyId });

                if (!string.IsNullOrEmpty(coName))
                    entries.Add(new DriverEntry { Name = coName, Number = coNum ?? string.Empty, Type = "CoPilot", RallyName = rallyName, RallyId = rallyId });
            }

            _debug.Log($"{entries.Count} inschrijvingen geladen voor {rallyName}.");
            return entries;
        }
        catch (Exception ex)
        {
            _debug.Log($"Fout bij ophalen inschrijvingen voor {rallyName}: {ex.Message}");
            throw new Exception($"Kan inschrijvingen niet ophalen voor {rallyName}: {ex.Message}", ex);
        }
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
}

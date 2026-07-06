using EwrcScraper.Models;
using Newtonsoft.Json;

namespace EwrcScraper.Services;

// Owns member-stats.json: the cached eWRC statistics snapshot per eWRC ID.
// Keeps eWRC load minimal by only re-fetching entries older than a staleness
// window (or when the caller forces a full refresh), throttled between calls.
public class MemberStatsService
{
    private static readonly string CachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EwrcScraper", "member-stats.json");

    private readonly EwrcApiService _api;
    private readonly DebugService _debug;

    // Don't re-fetch an ID that was refreshed within this window unless forced.
    public TimeSpan Staleness { get; set; } = TimeSpan.FromDays(7);

    // Politeness delay between consecutive eWRC calls during a bulk refresh.
    private static readonly TimeSpan CallDelay = TimeSpan.FromMilliseconds(250);

    public MemberStatsService(EwrcApiService api, DebugService debug)
    {
        _api = api;
        _debug = debug;
    }

    public List<MemberStats> Load()
    {
        if (!File.Exists(CachePath)) return new List<MemberStats>();
        try
        {
            var json = File.ReadAllText(CachePath);
            return JsonConvert.DeserializeObject<List<MemberStats>>(json) ?? new List<MemberStats>();
        }
        catch (Exception ex)
        {
            _debug.Log($"Kan statistiek-cache niet laden: {ex.Message}");
            return new List<MemberStats>();
        }
    }

    public void Save(IEnumerable<MemberStats> stats)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            File.WriteAllText(CachePath, JsonConvert.SerializeObject(stats, Formatting.Indented));
        }
        catch (Exception ex)
        {
            _debug.Log($"Kan statistiek-cache niet opslaan: {ex.Message}");
        }
    }

    public DateTime? LaatstBijgewerkt()
    {
        var stats = Load();
        return stats.Count == 0 ? null : stats.Max(s => s.LaatstBijgewerkt);
    }

    // (ewrcId, rol, ledenNr) tuples to refresh. Rol is "driver" or "codriver".
    public record IdRef(string EwrcId, string Rol, string LedenNr);

    // Refreshes the cache for the given IDs. Only stale (or, when force=true, all)
    // entries actually hit the API. Progress is reported per completed ID.
    // Returns the full updated cache.
    public async Task<List<MemberStats>> RefreshAsync(
        IReadOnlyList<IdRef> ids,
        bool force,
        IProgress<(int done, int total, string naam)>? progress = null,
        CancellationToken ct = default)
    {
        var cache = Load().ToDictionary(s => Key(s.EwrcId, s.Rol), s => s);

        // Decide which IDs actually need fetching.
        var teVerversen = ids.Where(r =>
        {
            if (force) return true;
            if (!cache.TryGetValue(Key(r.EwrcId, r.Rol), out var bestaand)) return true;
            return DateTime.Now - bestaand.LaatstBijgewerkt > Staleness;
        }).ToList();

        _debug.Log($"Statistieken verversen: {teVerversen.Count} van {ids.Count} ID's (rest is actueel).");

        int done = 0;
        foreach (var r in teVerversen)
        {
            ct.ThrowIfCancellationRequested();

            var vers = await _api.GetMemberStatsAsync(r.EwrcId, r.Rol);
            done++;

            if (vers != null)
            {
                vers.LedenNr = r.LedenNr;

                // Carry the previous snapshot forward so we can detect fresh crossings.
                if (cache.TryGetValue(Key(r.EwrcId, r.Rol), out var oud))
                {
                    vers.VorigeStarts = oud.Starts;
                    vers.VorigeKilometers = oud.KilometersTotaal;
                }
                else
                {
                    // First time we see this ID: treat current as baseline (no false "Nieuw").
                    vers.VorigeStarts = vers.Starts;
                    vers.VorigeKilometers = vers.KilometersTotaal;
                }

                vers.LaatstBijgewerkt = DateTime.Now;
                cache[Key(r.EwrcId, r.Rol)] = vers;
                progress?.Report((done, teVerversen.Count, vers.Naam));
            }
            else
            {
                progress?.Report((done, teVerversen.Count, $"ID {r.EwrcId} (niet gevonden)"));
            }

            if (done < teVerversen.Count)
                await Task.Delay(CallDelay, ct);
        }

        var result = cache.Values.OrderBy(s => s.Naam).ToList();
        Save(result);
        return result;
    }

    private static string Key(string ewrcId, string rol) =>
        $"{rol.ToLowerInvariant()}:{ewrcId}";
}

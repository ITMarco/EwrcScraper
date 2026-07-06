namespace EwrcScraper.Models;

// One cached eWRC statistics snapshot per eWRC ID (a member can have two:
// one as driver/pilot and one as codriver/navigator). Persisted to member-stats.json
// so we only hit the eWRC API when data is stale or a manual refresh is requested.
public class MemberStats
{
    public string EwrcId { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;        // "driver" or "codriver"
    public string LedenNr { get; set; } = string.Empty;    // back-reference to the RCH member
    public string Naam { get; set; } = string.Empty;

    // Completed rally starts (results-based lg_starts) — the count that milestones use.
    public int Starts { get; set; }
    // Entries including any registered-but-not-yet-driven rallies (profile number_of_starts).
    // Ingeschreven - Starts = number of pending (upcoming, already registered) rallies.
    public int Ingeschreven { get; set; }
    public int KilometersTotaal { get; set; }
    public int EersteSeizoen { get; set; }
    public int LaatsteSeizoen { get; set; }
    public string Geboren { get; set; } = string.Empty;
    public int AantalTitels { get; set; }

    // Previous snapshot values — used to detect a freshly crossed milestone
    // between two refreshes (the "smart comparison" behaviour).
    public int VorigeStarts { get; set; }
    public int VorigeKilometers { get; set; }

    public DateTime LaatstBijgewerkt { get; set; }

    public string RolNl => Rol.Equals("codriver", StringComparison.OrdinalIgnoreCase) ? "Bijrijder" : "Rijder";

    public string ProfielUrl
    {
        get
        {
            if (string.IsNullOrEmpty(EwrcId)) return string.Empty;
            var segment = Rol.Equals("codriver", StringComparison.OrdinalIgnoreCase) ? "coprofile" : "profile";
            return $"https://www.ewrc-results.com/{segment}/{EwrcId}/";
        }
    }
}

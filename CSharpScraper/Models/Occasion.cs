namespace EwrcScraper.Models;

// Editable milestone thresholds, persisted to occasions.json.
public class OccasionSettings
{
    // Rally-start counts worth celebrating.
    public List<int> StartMijlpalen { get; set; } = new() { 10, 25, 50, 75, 100, 150, 200 };

    // Total-kilometre thresholds worth celebrating.
    public List<int> KilometerMijlpalen { get; set; } = new() { 5000, 10000, 25000, 50000, 100000 };

    // "Years since debut season" worth celebrating.
    public List<int> JubileumJaren { get; set; } = new() { 5, 10, 15, 20, 25, 30 };

    // Round ages worth celebrating (birthday).
    public List<int> LeeftijdMijlpalen { get; set; } = new() { 40, 50, 60, 65, 70, 75, 80 };

    // Flag a start milestone as "upcoming" when the member is this many starts away.
    public int StartVooraankondiging { get; set; } = 1;

    // Flag a km milestone as "upcoming" when the member is within this many km.
    public int KilometerVooraankondiging { get; set; } = 500;

    // Announce an upcoming birthday this many days ahead.
    public int VerjaardagVooraankondigingDagen { get; set; } = 14;

    // Mark a member as "less active" when their last rally season is more than
    // this many years ago.
    public int InactiefNaJaren { get; set; } = 3;
}

public enum OccasionSoort { RallyMijlpaal, KilometerMijlpaal, Jubileum, Verjaardag }
public enum OccasionStatus { Nieuw, Bereikt, Aankomend }

// A computed celebration row shown on the Occasions tab.
public class Occasion
{
    public string Naam { get; set; } = string.Empty;
    public string EwrcId { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;        // "Rijder" / "Bijrijder"
    public OccasionSoort Soort { get; set; }
    public OccasionStatus Status { get; set; }
    public string Omschrijving { get; set; } = string.Empty;
    public string ProfielUrl { get; set; } = string.Empty;

    public int LaatsteSeizoen { get; set; }
    public bool MinderActief { get; set; }

    public string LaatsteRally => LaatsteSeizoen > 0 ? LaatsteSeizoen.ToString() : "?";
    public string ActiviteitNl => MinderActief ? $"⚠ minder actief" : "actief";

    // Lower sorts first: Nieuw (0) → Aankomend (1) → Bereikt (2).
    public int StatusVolgorde => Status switch
    {
        OccasionStatus.Nieuw => 0,
        OccasionStatus.Aankomend => 1,
        _ => 2
    };

    public string SoortNl => Soort switch
    {
        OccasionSoort.RallyMijlpaal => "Rally-mijlpaal",
        OccasionSoort.KilometerMijlpaal => "Km-mijlpaal",
        OccasionSoort.Jubileum => "Jubileum",
        OccasionSoort.Verjaardag => "Verjaardag",
        _ => ""
    };

    public string StatusNl => Status switch
    {
        OccasionStatus.Nieuw => "🎉 Nieuw!",
        OccasionStatus.Aankomend => "⏳ Aankomend",
        OccasionStatus.Bereikt => "✓ Bereikt",
        _ => ""
    };
}

using EwrcScraper.Models;
using Newtonsoft.Json;

namespace EwrcScraper.Services;

// Owns occasions.json (editable milestone thresholds) and turns cached
// member statistics into a list of celebration occasions.
public class OccasionService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EwrcScraper", "occasions.json");

    private readonly DebugService _debug;

    public OccasionService(DebugService debug)
    {
        _debug = debug;
    }

    public OccasionSettings LoadSettings()
    {
        if (!File.Exists(SettingsPath)) return new OccasionSettings();
        try
        {
            var json = File.ReadAllText(SettingsPath);
            return JsonConvert.DeserializeObject<OccasionSettings>(json) ?? new OccasionSettings();
        }
        catch (Exception ex)
        {
            _debug.Log($"Kan mijlpaal-instellingen niet laden: {ex.Message}");
            return new OccasionSettings();
        }
    }

    public void SaveSettings(OccasionSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(settings, Formatting.Indented));
        }
        catch (Exception ex)
        {
            _debug.Log($"Kan mijlpaal-instellingen niet opslaan: {ex.Message}");
        }
    }

    // A member's registration for an upcoming (already-checked) rally.
    public record Registratie(string RallyNaam, string Datum);

    public static string RegistratieSleutel(string rol, string ewrcId) =>
        $"{rol.ToLowerInvariant()}:{ewrcId}";

    // Builds the celebration list for the whole club from the cached stats.
    // registraties (optional): eWRC "rol:id" → the checked rally the member is entered for,
    // used to name the rally when an upcoming start hits a milestone.
    public List<Occasion> BerekenOccasions(
        IEnumerable<MemberStats> stats,
        OccasionSettings s,
        IReadOnlyDictionary<string, Registratie>? registraties = null)
    {
        var occasions = new List<Occasion>();
        var vandaag = DateTime.Today;
        int huidigJaar = vandaag.Year;

        foreach (var m in stats)
        {
            if (string.IsNullOrEmpty(m.EwrcId)) continue;

            bool minderActief = m.LaatsteSeizoen > 0
                && (huidigJaar - m.LaatsteSeizoen) > s.InactiefNaJaren;

            Registratie? registratie = null;
            registraties?.TryGetValue(RegistratieSleutel(m.Rol, m.EwrcId), out registratie);

            var voorMember = new List<Occasion>();

            // Factual milestones (actually driven) always count, even for a role someone
            // has since stepped away from.
            VoegRallyMijlpalenToe(voorMember, m, s, registratie);
            VoegKilometerMijlpalenToe(voorMember, m, s);

            // Anniversary/birthday are only meaningful for a role that is still active —
            // otherwise you'd celebrate e.g. "20 jaar als bijrijder" for someone who
            // last navigated years ago. Suppress them for a less-active role.
            if (!minderActief)
            {
                VoegJubileumToe(voorMember, m, s, huidigJaar);
                VoegVerjaardagToe(voorMember, m, s, vandaag);
            }

            // Stamp activity info on every occasion this member produced.
            foreach (var o in voorMember)
            {
                o.LaatsteSeizoen = m.LaatsteSeizoen;
                o.MinderActief = minderActief;
            }
            occasions.AddRange(voorMember);
        }

        return occasions
            .OrderBy(o => o.StatusVolgorde)
            .ThenBy(o => o.MinderActief)   // active members first within a status
            .ThenBy(o => o.Naam)
            .ToList();
    }

    // Rally-start milestones. Uses the COMPLETED count (m.Starts) so a rally that is
    // only registered for (counted in m.Ingeschreven but not yet driven) never shows as
    // "reached". Milestones that fall in the pending range (Starts, Ingeschreven] mean the
    // member is already entered for the rally that will be that milestone start.
    private static void VoegRallyMijlpalenToe(List<Occasion> list, MemberStats m, OccasionSettings s, Registratie? registratie)
    {
        foreach (var mijlpaal in s.StartMijlpalen.OrderBy(x => x))
        {
            // 1. Just crossed since the previous snapshot → the standout "Nieuw!" case.
            if (m.VorigeStarts < mijlpaal && m.Starts >= mijlpaal)
            {
                list.Add(Maak(m, OccasionSoort.RallyMijlpaal, OccasionStatus.Nieuw,
                    $"{mijlpaal}e rallystart bereikt! (nu {m.Starts})"));
            }
            // 2. Sitting exactly on the milestone (already known, still worth showing).
            else if (m.Starts == mijlpaal)
            {
                list.Add(Maak(m, OccasionSoort.RallyMijlpaal, OccasionStatus.Bereikt,
                    $"{mijlpaal}e rallystart voltooid"));
            }
            // 3. Already registered for the rally that will be this milestone start.
            else if (m.Starts < mijlpaal && mijlpaal <= m.Ingeschreven)
            {
                if (registratie != null)
                {
                    list.Add(Maak(m, OccasionSoort.RallyMijlpaal, OccasionStatus.Nieuw,
                        $"🏁 {mijlpaal}e start bij {registratie.RallyNaam} op {FormatteerDatum(registratie.Datum)}!"));
                }
                else
                {
                    list.Add(Maak(m, OccasionSoort.RallyMijlpaal, OccasionStatus.Aankomend,
                        $"Al ingeschreven — wordt de {mijlpaal}e start"));
                }
            }
            // 4. Close by, but not entered yet: still needs to register for X more rallies.
            else if (m.Ingeschreven < mijlpaal && mijlpaal - m.Ingeschreven <= s.StartVooraankondiging)
            {
                int tekort = mijlpaal - m.Ingeschreven;
                var tekst = tekort == 1
                    ? $"Volgende rally wordt de {mijlpaal}e start!"
                    : $"Nog {tekort} rally's tot de {mijlpaal}e start";
                list.Add(Maak(m, OccasionSoort.RallyMijlpaal, OccasionStatus.Aankomend, tekst));
            }
        }
    }

    private static string FormatteerDatum(string ruw) =>
        DateTime.TryParse(ruw, out var d) ? d.ToString("d MMMM", Nl) : ruw;

    private static void VoegKilometerMijlpalenToe(List<Occasion> list, MemberStats m, OccasionSettings s)
    {
        foreach (var mijlpaal in s.KilometerMijlpalen.OrderBy(x => x))
        {
            if (m.VorigeKilometers < mijlpaal && m.KilometersTotaal >= mijlpaal)
            {
                list.Add(Maak(m, OccasionSoort.KilometerMijlpaal, OccasionStatus.Nieuw,
                    $"{mijlpaal:N0} km voorbij! (nu {m.KilometersTotaal:N0} km)"));
            }
            else if (m.KilometersTotaal < mijlpaal && mijlpaal - m.KilometersTotaal <= s.KilometerVooraankondiging)
            {
                int tekort = mijlpaal - m.KilometersTotaal;
                list.Add(Maak(m, OccasionSoort.KilometerMijlpaal, OccasionStatus.Aankomend,
                    $"Nog {tekort:N0} km tot {mijlpaal:N0} km"));
            }
        }
    }

    private static void VoegJubileumToe(List<Occasion> list, MemberStats m, OccasionSettings s, int huidigJaar)
    {
        if (m.EersteSeizoen <= 0) return;
        int jarenActief = huidigJaar - m.EersteSeizoen;
        if (jarenActief <= 0) return;

        if (s.JubileumJaren.Contains(jarenActief))
        {
            // The Nth-year anniversary falls in EersteSeizoen + N.
            int bereiktJaar = m.EersteSeizoen + jarenActief;
            list.Add(Maak(m, OccasionSoort.Jubileum, OccasionStatus.Bereikt,
                $"{jarenActief} jaar actief in de rallysport — bereikt in {bereiktJaar} (debuut {m.EersteSeizoen})"));
        }
    }

    private static readonly System.Globalization.CultureInfo Nl = new("nl-NL");

    private static void VoegVerjaardagToe(List<Occasion> list, MemberStats m, OccasionSettings s, DateTime vandaag)
    {
        if (!DateTime.TryParse(m.Geboren, out var geboren)) return;

        int leeftijdDitJaar = vandaag.Year - geboren.Year;   // age they turn this calendar year
        if (leeftijdDitJaar <= 0) return;

        // This year's birthday date, guarding against 29 Feb on a non-leap year.
        int dag = geboren.Day;
        if (geboren.Month == 2 && dag == 29 && !DateTime.IsLeapYear(vandaag.Year)) dag = 28;
        var verjaardag = new DateTime(vandaag.Year, geboren.Month, dag);

        int dagenTot = (verjaardag - vandaag).Days;
        bool isMijlpaalLeeftijd = s.LeeftijdMijlpalen.Contains(leeftijdDitJaar);
        string datum = verjaardag.ToString("d MMMM", Nl);

        // Coming up within the announcement window.
        if (dagenTot >= 0 && dagenTot <= s.VerjaardagVooraankondigingDagen)
        {
            var status = isMijlpaalLeeftijd ? OccasionStatus.Nieuw : OccasionStatus.Aankomend;
            var tekst = isMijlpaalLeeftijd
                ? $"Wordt {leeftijdDitJaar} op {datum}! 🎂"
                : $"Jarig op {datum} — wordt {leeftijdDitJaar}";
            list.Add(Maak(m, OccasionSoort.Verjaardag, status, tekst));
        }
        // A round-age birthday elsewhere in the year is still worth flagging.
        else if (isMijlpaalLeeftijd)
        {
            if (dagenTot < 0)
                list.Add(Maak(m, OccasionSoort.Verjaardag, OccasionStatus.Bereikt,
                    $"Werd dit jaar {leeftijdDitJaar} (verjaardag was {datum})"));
            else
                list.Add(Maak(m, OccasionSoort.Verjaardag, OccasionStatus.Aankomend,
                    $"Wordt dit jaar {leeftijdDitJaar} (verjaardag {datum})"));
        }
    }

    private static Occasion Maak(MemberStats m, OccasionSoort soort, OccasionStatus status, string omschrijving) =>
        new()
        {
            Naam = string.IsNullOrEmpty(m.Naam) ? $"ID {m.EwrcId}" : m.Naam,
            EwrcId = m.EwrcId,
            Rol = m.RolNl,
            Soort = soort,
            Status = status,
            Omschrijving = omschrijving,
            ProfielUrl = m.ProfielUrl,
        };
}

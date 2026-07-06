using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EwrcScraper.Models;
using EwrcScraper.Services;
using System.Collections.ObjectModel;

namespace EwrcScraper.ViewModels;

public partial class OccasionsViewModel : ObservableObject
{
    private readonly MemberListViewModel _leden;
    private readonly RallySelectionViewModel _rally;
    private readonly EwrcApiService _api;
    private readonly MemberStatsService _statsService;
    private readonly OccasionService _occasionService;
    private readonly DebugService _debug;

    private List<MemberStats> _stats = new();
    private OccasionSettings _settings = new();

    // Cross-reference: eWRC "rol:id" → the checked rally the member is entered for.
    private Dictionary<string, OccasionService.Registratie> _registraties = new();

    // All computed occasions, before the "alleen nieuw/aankomend" filter.
    private List<Occasion> _alle = new();

    public ObservableCollection<Occasion> Occasions { get; } = new();

    [ObservableProperty]
    private string _statusTekst = "Nog geen statistieken geladen.";

    [ObservableProperty]
    private string _laatstBijgewerkt = string.Empty;

    [ObservableProperty]
    private bool _isBezig;

    [ObservableProperty]
    private double _voortgang;

    [ObservableProperty]
    private bool _alleenTeVieren;

    [ObservableProperty]
    private string _kruisStatus = string.Empty;

    public OccasionsViewModel(
        MemberListViewModel leden,
        RallySelectionViewModel rally,
        EwrcApiService api,
        MemberStatsService statsService,
        OccasionService occasionService,
        DebugService debug)
    {
        _leden = leden;
        _rally = rally;
        _api = api;
        _statsService = statsService;
        _occasionService = occasionService;
        _debug = debug;
    }

    // Loads cached data at startup without touching the network.
    public void LaadUitCache()
    {
        _settings = _occasionService.LoadSettings();
        _stats = _statsService.Load();
        HerberekenEnToon();
        WerkTijdstipBij();

        if (_stats.Count == 0)
            StatusTekst = "Nog geen statistieken. Klik 'Ververs statistieken' om ze op te halen van eWRC.";
    }

    partial void OnAlleenTeVierenChanged(bool value) => ToonGefilterd();

    [RelayCommand(CanExecute = nameof(KanVerversen))]
    private Task VerversStale() => VerversAsync(force: false);

    [RelayCommand(CanExecute = nameof(KanVerversen))]
    private Task VerversAlles() => VerversAsync(force: true);

    private bool KanVerversen() => !IsBezig;

    private async Task VerversAsync(bool force)
    {
        var refs = BouwIdRefs();
        if (refs.Count == 0)
        {
            StatusTekst = "Geen leden met een eWRC-ID gevonden. Laad eerst een ledenlijst (Stap 2).";
            return;
        }

        IsBezig = true;
        VerversStaleCommand.NotifyCanExecuteChanged();
        VerversAllesCommand.NotifyCanExecuteChanged();
        KruisMetRallyCommand.NotifyCanExecuteChanged();
        Voortgang = 0;
        StatusTekst = force ? "Alle statistieken verversen..." : "Verouderde statistieken verversen...";

        try
        {
            var progress = new Progress<(int done, int total, string naam)>(p =>
            {
                Voortgang = p.total == 0 ? 100 : (double)p.done / p.total * 100;
                StatusTekst = $"Ophalen {p.done}/{p.total}: {p.naam}";
            });

            _stats = await _statsService.RefreshAsync(refs, force, progress);
            _settings = _occasionService.LoadSettings();
            HerberekenEnToon();
            WerkTijdstipBij();

            int teVieren = _alle.Count(o => o.Status != OccasionStatus.Bereikt);
            StatusTekst = $"Klaar. {_alle.Count} gelegenheden gevonden ({teVieren} nieuw/aankomend).";
        }
        catch (Exception ex)
        {
            StatusTekst = $"Fout bij verversen: {ex.Message}";
            _debug.Log($"Occasions verversen mislukt: {ex.Message}");
        }
        finally
        {
            IsBezig = false;
            Voortgang = 0;
            VerversStaleCommand.NotifyCanExecuteChanged();
            VerversAllesCommand.NotifyCanExecuteChanged();
            KruisMetRallyCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private void OpenProfiel(Occasion? occasion)
    {
        if (occasion == null || string.IsNullOrEmpty(occasion.ProfielUrl)) return;
        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(occasion.ProfielUrl) { UseShellExecute = true });
    }

    // Re-reads settings after the editor closes and recomputes without a network call.
    public void HerlaadInstellingen()
    {
        _settings = _occasionService.LoadSettings();
        HerberekenEnToon();
    }

    public OccasionSettings HuidigeInstellingen() => _occasionService.LoadSettings();

    private List<MemberStatsService.IdRef> BouwIdRefs()
    {
        var refs = new List<MemberStatsService.IdRef>();
        foreach (var lid in _leden.Leden)
        {
            if (!string.IsNullOrWhiteSpace(lid.EwrcNrPilot))
                refs.Add(new MemberStatsService.IdRef(lid.EwrcNrPilot.Trim(), "driver", lid.LedenNr));
            if (!string.IsNullOrWhiteSpace(lid.EwrcNrCoPilot))
                refs.Add(new MemberStatsService.IdRef(lid.EwrcNrCoPilot.Trim(), "codriver", lid.LedenNr));
        }
        return refs;
    }

    private void HerberekenEnToon()
    {
        _alle = _occasionService.BerekenOccasions(_stats, _settings, _registraties);
        ToonGefilterd();
    }

    // Cross-references the rally/rallies checked in Stap 1 with the members: it fetches
    // each checked rally's entry list and remembers which members are entered, so an
    // upcoming milestone start can be tied to a specific rally and date.
    [RelayCommand(CanExecute = nameof(KanVerversen))]
    private async Task KruisMetRally()
    {
        var events = _rally.GeselecteerdeEvents.ToList();
        if (events.Count == 0)
        {
            KruisStatus = "Vink eerst een rally aan in Stap 1.";
            return;
        }
        if (_stats.Count == 0)
        {
            KruisStatus = "Ververs eerst de statistieken.";
            return;
        }

        IsBezig = true;
        VerversStaleCommand.NotifyCanExecuteChanged();
        VerversAllesCommand.NotifyCanExecuteChanged();
        KruisMetRallyCommand.NotifyCanExecuteChanged();

        try
        {
            var ledenSleutels = new HashSet<string>(
                _stats.Select(s => OccasionService.RegistratieSleutel(s.Rol, s.EwrcId)));
            var nieuw = new Dictionary<string, OccasionService.Registratie>();

            foreach (var ev in events)
            {
                StatusTekst = $"Inschrijvingen ophalen: {ev.Name}...";
                var entries = await _api.GetEntriesAsync(ev.Id, ev.Name);
                foreach (var e in entries)
                {
                    var rol = e.Type == "Driver" ? "driver" : "codriver";
                    var sleutel = OccasionService.RegistratieSleutel(rol, e.Number);
                    if (ledenSleutels.Contains(sleutel))
                        nieuw[sleutel] = new OccasionService.Registratie(ev.Name, ev.From);
                }
            }

            _registraties = nieuw;
            HerberekenEnToon();
            KruisStatus = $"Gekruist met {events.Count} rally('s): {_registraties.Count} leden-inschrijving(en).";
            StatusTekst = KruisStatus;
        }
        catch (Exception ex)
        {
            KruisStatus = $"Kruisen mislukt: {ex.Message}";
            _debug.Log($"Kruisen met rally mislukt: {ex.Message}");
        }
        finally
        {
            IsBezig = false;
            VerversStaleCommand.NotifyCanExecuteChanged();
            VerversAllesCommand.NotifyCanExecuteChanged();
            KruisMetRallyCommand.NotifyCanExecuteChanged();
        }
    }

    private void ToonGefilterd()
    {
        Occasions.Clear();
        var bron = AlleenTeVieren
            ? _alle.Where(o => o.Status != OccasionStatus.Bereikt)
            : _alle;
        foreach (var o in bron) Occasions.Add(o);
    }

    private void WerkTijdstipBij()
    {
        var laatst = _statsService.LaatstBijgewerkt();
        LaatstBijgewerkt = laatst == null
            ? "Statistieken nog niet opgehaald"
            : $"Statistieken bijgewerkt: {laatst:dd-MM-yyyy HH:mm}";
    }
}

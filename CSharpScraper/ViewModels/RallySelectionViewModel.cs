using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EwrcScraper.Models;
using EwrcScraper.Services;
using System.Collections.ObjectModel;

namespace EwrcScraper.ViewModels;

public partial class RallySelectionViewModel : ObservableObject
{
    private readonly EwrcApiService _api;
    private readonly DebugService _debug;
    private readonly PreferencesService _prefs;

    public ObservableCollection<Country> Landen { get; } = new();
    public ObservableCollection<RallyEvent> RallyEvents { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GefilterdeEvents))]
    private string _filter = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GefilterdeEvents))]
    private string _sortering = "land";

    [ObservableProperty]
    private bool _landenUitgevouwen = true;

    [ObservableProperty]
    private bool _isBezig;

    [ObservableProperty]
    private string _statusTekst = string.Empty;

    [ObservableProperty]
    private int _geselecteerdJaar = DateTime.Now.Year;

    public IEnumerable<RallyEvent> GefilterdeEvents
    {
        get
        {
            var events = string.IsNullOrWhiteSpace(Filter)
                ? RallyEvents.AsEnumerable()
                : RallyEvents.Where(r => r.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase));
            return Sortering switch
            {
                "datumAsc"  => events.OrderBy(r => r.From),
                "datumDesc" => events.OrderByDescending(r => r.From),
                _           => events  // "land": keep API order (already per country)
            };
        }
    }

    public IEnumerable<RallyEvent> GeselecteerdeEvents =>
        RallyEvents.Where(r => r.IsSelected);

    public List<int> GeselecteerdeLandIds =>
        Landen.Where(l => l.IsSelected).Select(l => l.Id).ToList();

    public string GeselecteerdeLandenSamenvatting
    {
        get
        {
            var sel = Landen.Where(l => l.IsSelected).ToList();
            if (sel.Count == 0) return "Geen landen geselecteerd";
            return string.Join("\n", sel.Select(l => $"{l.Flag} {l.Name}"));
        }
    }

    public RallySelectionViewModel(EwrcApiService api, DebugService debug, PreferencesService prefs)
    {
        _api = api;
        _debug = debug;
        _prefs = prefs;
    }

    public async Task LaadLandenAsync(List<int> geselecteerdeIds)
    {
        IsBezig = true;
        StatusTekst = "Landen laden...";
        try
        {
            _debug.Log($"Landen laden: opgeslagen selectie = [{string.Join(",", geselecteerdeIds)}]");
            var landen = await _api.GetCountriesAsync(GeselecteerdJaar);
            Landen.Clear();
            foreach (var land in landen.OrderBy(l => l.Name))
            {
                land.IsSelected = geselecteerdeIds.Contains(land.Id);
                land.PropertyChanged += (_, _) =>
                {
                    OnPropertyChanged(nameof(GeselecteerdeLandIds));
                    OnPropertyChanged(nameof(GeselecteerdeLandenSamenvatting));
                    _prefs.SaveCountryIds(GeselecteerdeLandIds);
                };
                Landen.Add(land);
            }
            // Notify after initial selection is set (PropertyChanged is wired AFTER IsSelected is set above)
            OnPropertyChanged(nameof(GeselecteerdeLandIds));
            OnPropertyChanged(nameof(GeselecteerdeLandenSamenvatting));

            var geselecteerd = Landen.Where(l => l.IsSelected).ToList();
            _debug.Log($"Landen geladen: {Landen.Count} totaal, {geselecteerd.Count} geselecteerd: [{string.Join(", ", geselecteerd.Select(l => $"{l.Name}({l.Id})"))}]");
            StatusTekst = $"{landen.Count} landen geladen.";
        }
        catch (Exception ex)
        {
            StatusTekst = $"Fout: {ex.Message}";
        }
        finally
        {
            IsBezig = false;
        }
    }

    [RelayCommand(CanExecute = nameof(KanRallyLijstUpdaten))]
    private async Task UpdateRallyLijst()
    {
        var ids = GeselecteerdeLandIds;
        if (ids.Count == 0)
        {
            StatusTekst = "Selecteer eerst een of meer landen.";
            return;
        }

        IsBezig = true;
        StatusTekst = "Rally's ophalen...";
        try
        {
            var events = await _api.GetCalendarAsync(GeselecteerdJaar, ids);
            RallyEvents.Clear();
            foreach (var ev in events)
            {
                ev.PropertyChanged += (_, _) => OnPropertyChanged(nameof(GeselecteerdeEvents));
                RallyEvents.Add(ev);
            }
            OnPropertyChanged(nameof(GefilterdeEvents));
            StatusTekst = $"{events.Count} rally's geladen.";
        }
        catch (Exception ex)
        {
            StatusTekst = $"Fout: {ex.Message}";
        }
        finally
        {
            IsBezig = false;
        }
    }

    private bool KanRallyLijstUpdaten() => !IsBezig;

    [RelayCommand]
    private void SelecteerAlle() { foreach (var r in RallyEvents) r.IsSelected = true; }

    [RelayCommand]
    private void DeselecteerAlle() { foreach (var r in RallyEvents) r.IsSelected = false; }

    [RelayCommand]
    private void SorteerOpLand() => Sortering = "land";

    [RelayCommand]
    private void SorteerOpDatumAsc() => Sortering = "datumAsc";

    [RelayCommand]
    private void SorteerOpDatumDesc() => Sortering = "datumDesc";

    partial void OnFilterChanged(string value) => OnPropertyChanged(nameof(GefilterdeEvents));
}

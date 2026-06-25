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

    public ObservableCollection<Country> Landen { get; } = new();
    public ObservableCollection<RallyEvent> RallyEvents { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GefilterdeEvents))]
    private string _filter = string.Empty;

    [ObservableProperty]
    private bool _isBezig;

    [ObservableProperty]
    private string _statusTekst = string.Empty;

    [ObservableProperty]
    private int _geselecteerdJaar = DateTime.Now.Year;

    public IEnumerable<RallyEvent> GefilterdeEvents =>
        string.IsNullOrWhiteSpace(Filter)
            ? RallyEvents
            : RallyEvents.Where(r => r.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<RallyEvent> GeselecteerdeEvents =>
        RallyEvents.Where(r => r.IsSelected);

    public List<int> GeselecteerdeLandIds =>
        Landen.Where(l => l.IsSelected).Select(l => l.Id).ToList();

    public RallySelectionViewModel(EwrcApiService api, DebugService debug)
    {
        _api = api;
        _debug = debug;
    }

    public async Task LaadLandenAsync(List<int> geselecteerdeIds)
    {
        IsBezig = true;
        StatusTekst = "Landen laden...";
        try
        {
            var landen = await _api.GetCountriesAsync(GeselecteerdJaar);
            Landen.Clear();
            foreach (var land in landen.OrderBy(l => l.Name))
            {
                land.IsSelected = geselecteerdeIds.Contains(land.Id);
                land.PropertyChanged += (_, _) => OnPropertyChanged(nameof(GeselecteerdeLandIds));
                Landen.Add(land);
            }
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

    partial void OnFilterChanged(string value) => OnPropertyChanged(nameof(GefilterdeEvents));
}

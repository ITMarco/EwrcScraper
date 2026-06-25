using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EwrcScraper.Models;
using EwrcScraper.Services;

namespace EwrcScraper.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public RallySelectionViewModel RallySelectie { get; }
    public MemberListViewModel Ledenlijst { get; }
    public ComparisonViewModel Vergelijking { get; }
    public DriverSearchViewModel RijderZoeken { get; }
    public DebugViewModel Debug { get; }

    private readonly PreferencesService _prefsService;

    [ObservableProperty]
    private AppPreferences _voorkeuren;

    [ObservableProperty]
    private string _statusBalk = "Gereed.";

    [ObservableProperty]
    private bool _isBezig;

    public MainViewModel(
        RallySelectionViewModel rallySelectie,
        MemberListViewModel ledenlijst,
        ComparisonViewModel vergelijking,
        DriverSearchViewModel rijderZoeken,
        DebugViewModel debugVm,
        PreferencesService prefsService,
        DebugService debug)
    {
        RallySelectie = rallySelectie;
        Ledenlijst = ledenlijst;
        Vergelijking = vergelijking;
        RijderZoeken = rijderZoeken;
        Debug = debugVm;
        _prefsService = prefsService;
        _voorkeuren = prefsService.Load();

        debug.LogAdded += msg => StatusBalk = msg;
    }

    public async Task InitialiserenAsync()
    {
        var prefs = _prefsService.Load();
        Voorkeuren = prefs;

        await RallySelectie.LaadLandenAsync(prefs.GeselecteerdeCountryIds);

        if (!string.IsNullOrEmpty(prefs.LedenlijstPad))
            Ledenlijst.LaadVanPad(prefs.LedenlijstPad);
    }

    public void VoorkeurenOpslaan(double x, double y, double w, double h)
    {
        var prefs = _prefsService.Load();
        prefs.WindowX = x;
        prefs.WindowY = y;
        prefs.WindowWidth = w;
        prefs.WindowHeight = h;
        prefs.GeselecteerdeCountryIds = RallySelectie.GeselecteerdeLandIds;
        prefs.LedenlijstPad = Ledenlijst.LedenlijstPad;
        _prefsService.Save(prefs);

        // Log what was saved so the user can verify in the debug window
        var landen = string.Join(", ", prefs.GeselecteerdeCountryIds);
        StatusBalk = $"Voorkeuren opgeslagen. Landen: [{landen}]";
    }

    [RelayCommand]
    private async Task HaalRallyInfoOp()
    {
        await Vergelijking.HaalRallyInfoOp(RallySelectie.GeselecteerdeEvents);
    }

    [RelayCommand]
    private void Vergelijk()
    {
        Vergelijking.Vergelijk(Ledenlijst.Leden);
    }
}

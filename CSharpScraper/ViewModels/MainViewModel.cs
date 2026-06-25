using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EwrcScraper.Models;
using EwrcScraper.Services;
using System.Diagnostics;

namespace EwrcScraper.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public RallySelectionViewModel RallySelectie { get; }
    public MemberListViewModel Ledenlijst { get; }
    public ComparisonViewModel Vergelijking { get; }
    public DriverSearchViewModel RijderZoeken { get; }
    public DebugViewModel Debug { get; }

    public PreferencesService VoorkeurenService { get; }
    public UpdateService UpdateService { get; }

    public UpdateInfo? BeschikbareUpdate { get; private set; }

    private readonly PreferencesService _prefsService;

    [ObservableProperty]
    private AppPreferences _voorkeuren;

    [ObservableProperty]
    private string _statusBalk = "Gereed.";

    [ObservableProperty]
    private bool _isBezig;

    private readonly DebugService _debug;

    public MainViewModel(
        RallySelectionViewModel rallySelectie,
        MemberListViewModel ledenlijst,
        ComparisonViewModel vergelijking,
        DriverSearchViewModel rijderZoeken,
        DebugViewModel debugVm,
        PreferencesService prefsService,
        UpdateService updateService,
        DebugService debug)
    {
        _debug = debug;
        RallySelectie = rallySelectie;
        Ledenlijst = ledenlijst;
        Vergelijking = vergelijking;
        RijderZoeken = rijderZoeken;
        Debug = debugVm;
        _prefsService = prefsService;
        VoorkeurenService = prefsService;
        UpdateService = updateService;
        _voorkeuren = prefsService.Load();

        debug.LogAdded += msg => StatusBalk = msg;
    }

    public async Task InitialiserenAsync()
    {
        var prefsBestaatAl = File.Exists(PreferencesService.ConfigPath);
        var prefs = _prefsService.Load();
        Voorkeuren = prefs;

        await RallySelectie.LaadLandenAsync(prefs.GeselecteerdeCountryIds);

        if (!string.IsNullOrEmpty(prefs.LedenlijstPad))
            Ledenlijst.LaadVanPad(prefs.LedenlijstPad);

        // Auto-load rally list on subsequent runs; skip on very first run (no prefs file yet)
        if (prefsBestaatAl && prefs.GeselecteerdeCountryIds.Count > 0)
            await RallySelectie.UpdateRallyLijstCommand.ExecuteAsync(null);

        if (prefs.ControleerUpdates)
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "(onbekend)";
            var isInstalled = UpdateService.IsInstalledVersion();
            _debug.Log($"[Update] Versie: {UpdateService.HuidigeVersie()} | Exe: {exePath} | Geïnstalleerd: {isInstalled}");

            var (update, geslaagd) = await UpdateService.CheckForUpdateAsync();
            BeschikbareUpdate = update;

            if (!geslaagd)
                _debug.Log("[Update] Controle mislukt (netwerk of API-fout).");
            else if (update == null)
                _debug.Log("[Update] Al up-to-date.");
            else
                _debug.Log($"[Update] Nieuwe versie: {update.VersieNummer} | InstallerUrl: {update.InstallerDownloadUrl}");
        }
    }

    public void VoorkeurenOpslaan(double x, double y, double w, double h)
    {
        var prefs = _prefsService.Load();
        prefs.WindowX = x;
        prefs.WindowY = y;
        prefs.WindowWidth = w;
        prefs.WindowHeight = h;
        if (RallySelectie.Landen.Count > 0)
            prefs.GeselecteerdeCountryIds = RallySelectie.GeselecteerdeLandIds;
        prefs.LedenlijstPad = Ledenlijst.LedenlijstPad;
        _prefsService.Save(prefs);

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

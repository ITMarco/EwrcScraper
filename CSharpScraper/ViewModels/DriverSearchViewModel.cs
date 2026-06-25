using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EwrcScraper.Models;
using EwrcScraper.Services;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;

namespace EwrcScraper.ViewModels;

public partial class DriverSearchViewModel : ObservableObject
{
    private readonly EwrcApiService _api;
    private readonly DebugService _debug;

    public ObservableCollection<SearchResult> Zoekresultaten { get; } = new();
    public ObservableCollection<DriverCategoryStats> RijderStats { get; } = new();
    public ObservableCollection<DriverProfile> VergelijkLijst { get; } = new();
    public ObservableCollection<VergelijkRegel> VergelijkRegels { get; } = new();

    [ObservableProperty]
    private string _zoekterm = string.Empty;

    [ObservableProperty]
    private bool _isBezig;

    [ObservableProperty]
    private string _statusTekst = string.Empty;

    [ObservableProperty]
    private SearchResult? _geselecteerdeRijder;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VoegToeAanVergelijkingCommand))]
    private DriverProfile? _huidigProfiel;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartVergelijkingCommand))]
    private int _vergelijkAantal;

    [ObservableProperty]
    private bool _toonVergelijking;

    [ObservableProperty]
    private BitmapImage? _rijderFoto;

    [ObservableProperty]
    private string _rijderNaam = string.Empty;

    [ObservableProperty]
    private string _rijderLand = string.Empty;

    [ObservableProperty]
    private string _rijderGeboren = string.Empty;

    [ObservableProperty]
    private string _rijderType = string.Empty;

    [ObservableProperty]
    private string _rijderEwrcId = string.Empty;

    [ObservableProperty]
    private string _rijderProfielUrl = string.Empty;

    public DriverSearchViewModel(EwrcApiService api, DebugService debug)
    {
        _api = api;
        _debug = debug;
        VergelijkLijst.CollectionChanged += (_, _) =>
        {
            VergelijkAantal = VergelijkLijst.Count;
            StartVergelijkingCommand.NotifyCanExecuteChanged();
        };
    }

    [RelayCommand(CanExecute = nameof(KanZoeken))]
    private async Task Zoek()
    {
        if (string.IsNullOrWhiteSpace(Zoekterm)) return;
        IsBezig = true;
        StatusTekst = "Zoeken...";
        Zoekresultaten.Clear();
        ToonVergelijking = false;

        try
        {
            var results = await _api.SearchAsync(Zoekterm);
            foreach (var r in results) Zoekresultaten.Add(r);
            StatusTekst = results.Count > 0 ? $"{results.Count} resultaten gevonden." : "Geen resultaten gevonden.";
        }
        catch (Exception ex)
        {
            StatusTekst = $"Zoekfout: {ex.Message}";
        }
        finally
        {
            IsBezig = false;
        }
    }

    private bool KanZoeken() => !IsBezig && !string.IsNullOrWhiteSpace(Zoekterm);

    partial void OnZoektermChanged(string value) => ZoekCommand.NotifyCanExecuteChanged();

    partial void OnGeselecteerdeRijderChanged(SearchResult? value)
    {
        if (value != null)
            _ = LaadRijdersprofielAsync(value.Id, value.Type);
    }

    private async Task LaadRijdersprofielAsync(string id, string typeHint = "")
    {
        IsBezig = true;
        RijderStats.Clear();
        RijderFoto = null;
        HuidigProfiel = null;
        ToonVergelijking = false;

        try
        {
            var profiel = await _api.GetDriverProfileAsync(id, typeHint);

            // Profile endpoint has no type field; use what the search result told us
            if (string.IsNullOrEmpty(profiel.Type) && !string.IsNullOrEmpty(typeHint))
                profiel.Type = typeHint;

            HuidigProfiel = profiel;
            RijderEwrcId = profiel.Id;
            RijderProfielUrl = profiel.ProfileUrl;
            RijderNaam = profiel.Name;
            RijderLand = profiel.Country;
            RijderGeboren = profiel.Born;
            RijderType = profiel.Type;

            foreach (var stat in profiel.Stats) RijderStats.Add(stat);

            if (!string.IsNullOrEmpty(profiel.PhotoUrl))
            {
                var bytes = await _api.DownloadImageAsync(profiel.PhotoUrl);
                if (bytes != null) RijderFoto = BytesToBitmap(bytes);
            }

            StatusTekst = string.IsNullOrEmpty(profiel.Name)
                ? StatusTekst
                : $"Profiel geladen: {profiel.Name}";
        }
        catch (Exception ex)
        {
            StatusTekst = $"Fout profiel laden: {ex.Message}";
        }
        finally
        {
            IsBezig = false;
        }
    }

    [RelayCommand]
    private void OpenProfielUrl()
    {
        if (!string.IsNullOrEmpty(RijderProfielUrl))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(RijderProfielUrl) { UseShellExecute = true });
    }

    [RelayCommand(CanExecute = nameof(KanToevoegen))]
    private void VoegToeAanVergelijking()
    {
        if (HuidigProfiel == null) return;
        if (!VergelijkLijst.Any(r => r.Id == HuidigProfiel.Id))
            VergelijkLijst.Add(HuidigProfiel);
        StatusTekst = $"{HuidigProfiel.Name} toegevoegd aan vergelijkingslijst ({VergelijkLijst.Count}).";
    }

    private bool KanToevoegen() => HuidigProfiel != null;

    [RelayCommand]
    private void VerwijderUitVergelijking(DriverProfile profiel) => VergelijkLijst.Remove(profiel);

    [RelayCommand]
    private void WisVergelijking()
    {
        VergelijkLijst.Clear();
        ToonVergelijking = false;
    }

    [RelayCommand(CanExecute = nameof(KanVergelijken))]
    private void StartVergelijking()
    {
        VergelijkRegels.Clear();
        foreach (var p in VergelijkLijst)
        {
            // Sum across all categories (usually just one: "Rally")
            var starts = p.Stats.Sum(s => s.Starts);
            var uitval = p.Stats.Sum(s => s.Uitval);
            var uitvalPct = starts > 0 ? Math.Round(100.0 * uitval / starts, 0) : 0;
            VergelijkRegels.Add(new VergelijkRegel
            {
                EwrcId = p.Id,
                Naam = p.Name,
                Land = p.Country,
                Type = p.Type,
                Geboren = p.Born,
                Starts = starts,
                Uitval = uitvalPct > 0 ? $"{uitval} ({uitvalPct:F0}%)" : $"{uitval}",
                Winst = p.Stats.Sum(s => s.Wins),
                KlasseWinst = p.Stats.Sum(s => s.KlasseWinst),
            });
        }
        ToonVergelijking = true;
    }

    private bool KanVergelijken() => VergelijkLijst.Count >= 2;

    [RelayCommand]
    private void SluitVergelijking() => ToonVergelijking = false;

    [RelayCommand]
    private void ToonVergelijkRijder(DriverProfile profiel)
    {
        ToonVergelijking = false;
        HuidigProfiel = profiel;
        RijderEwrcId = profiel.Id;
        RijderProfielUrl = profiel.ProfileUrl;
        RijderNaam = profiel.Name;
        RijderLand = profiel.Country;
        RijderGeboren = profiel.Born;
        RijderType = profiel.Type;
        RijderStats.Clear();
        foreach (var stat in profiel.Stats) RijderStats.Add(stat);
        RijderFoto = null;
    }

    private static BitmapImage BytesToBitmap(byte[] bytes)
    {
        using var ms = new System.IO.MemoryStream(bytes);
        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.StreamSource = ms;
        img.EndInit();
        img.Freeze();
        return img;
    }
}

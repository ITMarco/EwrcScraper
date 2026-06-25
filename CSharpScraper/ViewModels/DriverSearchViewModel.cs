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

    [ObservableProperty]
    private string _zoekterm = string.Empty;

    [ObservableProperty]
    private bool _isBezig;

    [ObservableProperty]
    private string _statusTekst = string.Empty;

    [ObservableProperty]
    private SearchResult? _geselecteerdeRijder;

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

    public DriverSearchViewModel(EwrcApiService api, DebugService debug)
    {
        _api = api;
        _debug = debug;
    }

    [RelayCommand(CanExecute = nameof(KanZoeken))]
    private async Task Zoek()
    {
        if (string.IsNullOrWhiteSpace(Zoekterm)) return;
        IsBezig = true;
        StatusTekst = "Zoeken...";
        Zoekresultaten.Clear();
        RijderFoto = null;
        RijderStats.Clear();

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
            _ = LaadRijdersprofielAsync(value.Id);
    }

    private async Task LaadRijdersprofielAsync(string id)
    {
        IsBezig = true;
        RijderStats.Clear();
        RijderFoto = null;

        try
        {
            var profiel = await _api.GetDriverProfileAsync(id);
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

using CommunityToolkit.Mvvm.ComponentModel;
using EwrcScraper.Models;
using EwrcScraper.Services;

namespace EwrcScraper.ViewModels;

public partial class PreferencesViewModel : ObservableObject
{
    private readonly UpdateService _updateService;

    [ObservableProperty]
    private bool _controleerUpdates;

    [ObservableProperty]
    private bool _debugVensterBijOpstarten;

    [ObservableProperty]
    private string _apiBaseUrl = string.Empty;

    [ObservableProperty]
    private string _ledenlijstPad = string.Empty;

    [ObservableProperty]
    private string _updateStatusTekst = string.Empty;

    [ObservableProperty]
    private bool _isUpdateAanHetControleren;

    public string HuidigeVersie { get; }
    public UpdateService UpdateService => _updateService;

    public PreferencesViewModel(AppPreferences prefs, UpdateService updateService)
    {
        _updateService = updateService;
        ControleerUpdates = prefs.ControleerUpdates;
        DebugVensterBijOpstarten = prefs.DebugVensterZichtbaar;
        ApiBaseUrl = prefs.ApiBaseUrl;
        LedenlijstPad = prefs.LedenlijstPad;
        HuidigeVersie = updateService.HuidigeVersie();
    }

    public void ToepassenOp(AppPreferences prefs)
    {
        prefs.ControleerUpdates = ControleerUpdates;
        prefs.DebugVensterZichtbaar = DebugVensterBijOpstarten;
        prefs.ApiBaseUrl = ApiBaseUrl;
        prefs.LedenlijstPad = LedenlijstPad;
    }
}

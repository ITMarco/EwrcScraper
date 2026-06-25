using CommunityToolkit.Mvvm.ComponentModel;
using EwrcScraper.Models;

namespace EwrcScraper.ViewModels;

public partial class PreferencesViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _controleerUpdates;

    [ObservableProperty]
    private bool _debugVensterBijOpstarten;

    [ObservableProperty]
    private string _apiBaseUrl = string.Empty;

    public PreferencesViewModel(AppPreferences prefs)
    {
        ControleerUpdates = prefs.ControleerUpdates;
        DebugVensterBijOpstarten = prefs.DebugVensterZichtbaar;
        ApiBaseUrl = prefs.ApiBaseUrl;
    }

    public void ToepassenOp(AppPreferences prefs)
    {
        prefs.ControleerUpdates = ControleerUpdates;
        prefs.DebugVensterZichtbaar = DebugVensterBijOpstarten;
        prefs.ApiBaseUrl = ApiBaseUrl;
    }
}

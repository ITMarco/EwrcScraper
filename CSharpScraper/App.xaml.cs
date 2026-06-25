using EwrcScraper.Services;
using EwrcScraper.ViewModels;
using System.Net.Http;
using System.Windows;

namespace EwrcScraper;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var debug = new DebugService();
        var prefs = new PreferencesService();
        var http = new HttpClient();
        http.DefaultRequestHeaders.Add("User-Agent", "EwrcScraper/1.0 (RCH)");
        http.Timeout = TimeSpan.FromSeconds(30);

        var api = new EwrcApiService(http, debug);
        var memberSvc = new MemberListService(debug);

        var rallyVm = new RallySelectionViewModel(api, debug, prefs);
        var ledenVm = new MemberListViewModel(memberSvc, debug, prefs);
        var vergelijkVm = new ComparisonViewModel(api, debug);
        var rijderVm = new DriverSearchViewModel(api, debug);
        var debugVm = new DebugViewModel(debug, prefs);
        var mainVm = new MainViewModel(rallyVm, ledenVm, vergelijkVm, rijderVm, debugVm, prefs, debug);

        var window = new MainWindow(mainVm);
        window.Show();
    }
}

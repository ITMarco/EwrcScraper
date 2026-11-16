using EwrcScraper.Services;
using EwrcScraper.ViewModels;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;

namespace EwrcScraper;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Catch otherwise-fatal errors: log them and show the cause instead of a silent crash.
        DispatcherUnhandledException += (_, args) =>
        {
            LogCrash("UI-thread", args.Exception);
            MessageBox.Show(KorteFout(args.Exception), "EWRC Scraper — fout",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;   // keep the app alive
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            LogCrash("AppDomain", args.ExceptionObject as Exception);
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogCrash("Task", args.Exception);
            args.SetObserved();
        };

        var debug = new DebugService();
        var prefs = new PreferencesService();
        var geladen = prefs.Load();

        // eWRC sits behind Cloudflare, which blocks the .NET TLS fingerprint with 403
        // regardless of headers (verified: native curl.exe -> 200, .NET -> 403, same IP).
        // Route eWRC calls through curl.exe, whose TLS stack Cloudflare accepts; fall back
        // to the normal .NET stack only if curl.exe is somehow unavailable.
        HttpMessageHandler apiHandler = CurlHttpHandler.IsAvailable()
            ? new CurlHttpHandler(maxSeconds: 45)
            : new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };
        if (!(apiHandler is CurlHttpHandler))
            debug.Log("[Netwerk] curl.exe niet gevonden — val terug op .NET (mogelijk 403 van Cloudflare).");

        var apiHttp = new HttpClient(apiHandler);
        apiHttp.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36");
        apiHttp.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
        apiHttp.DefaultRequestHeaders.AcceptLanguage.ParseAdd("nl-NL,nl;q=0.9,en;q=0.8");
        apiHttp.DefaultRequestHeaders.Referrer = new Uri("https://ewrc-results.com/");
        apiHttp.DefaultRequestHeaders.Add("Origin", "https://ewrc-results.com");
        apiHttp.Timeout = TimeSpan.FromSeconds(60);

        // The GitHub update check is a separate service with a plain identifying client.
        var updateHttp = new HttpClient();
        updateHttp.DefaultRequestHeaders.Add("User-Agent", "EwrcScraper/1.0 (RCH)");
        updateHttp.Timeout = TimeSpan.FromSeconds(30);

        var api = new EwrcApiService(apiHttp, debug, geladen.ApiBaseUrl);
        var updateSvc = new UpdateService(updateHttp);
        var memberSvc = new MemberListService(debug);
        var statsSvc = new MemberStatsService(api, debug);
        var occasionSvc = new OccasionService(debug);

        var rallyVm = new RallySelectionViewModel(api, debug, prefs);
        var ledenVm = new MemberListViewModel(memberSvc, debug, prefs);
        var vergelijkVm = new ComparisonViewModel(api, debug);
        var rijderVm = new DriverSearchViewModel(api, debug);
        var occasionVm = new OccasionsViewModel(ledenVm, rallyVm, api, statsSvc, occasionSvc, debug);
        var debugVm = new DebugViewModel(debug, prefs);
        var mainVm = new MainViewModel(rallyVm, ledenVm, vergelijkVm, rijderVm, occasionVm, debugVm, prefs, updateSvc, occasionSvc, debug);

        var window = new MainWindow(mainVm);
        window.Show();
    }

    private static void LogCrash(string bron, Exception? ex)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EwrcScraper");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "crash.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {bron}\n{ex}\n\n");
        }
        catch { /* logging must never throw */ }
    }

    private static string KorteFout(Exception ex) =>
        $"Er ging iets mis:\n\n{ex.GetType().Name}: {ex.Message}\n\n" +
        $"Details staan in:\n%AppData%\\EwrcScraper\\crash.log";
}

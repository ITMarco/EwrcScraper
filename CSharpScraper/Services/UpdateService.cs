using EwrcScraper.Models;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;

namespace EwrcScraper.Services;

public class UpdateService
{
    private readonly HttpClient _http;
    private const string ReleasesUrl = "https://api.github.com/repos/ITMarco/EwrcScraper/releases/latest";

    public UpdateService(HttpClient http)
    {
        _http = http;
    }

    public string HuidigeVersie()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v == null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    // True when the running exe lives under Program Files — i.e. it was placed there
    // by the installer. False for the portable (framework-dependent) build run from elsewhere.
    public static bool IsInstalledVersion()
    {
        var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        if (string.IsNullOrEmpty(exePath)) return false;

        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        };
        return roots.Any(r => !string.IsNullOrEmpty(r)
            && exePath.StartsWith(r, StringComparison.OrdinalIgnoreCase));
    }

    // (update, true)  — newer version found
    // (null,   true)  — check succeeded, already up to date
    // (null,   false) — check failed (network, API error, etc.)
    public async Task<(UpdateInfo? Update, bool Geslaagd)> CheckForUpdateAsync()
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, ReleasesUrl);
            request.Headers.Add("Accept", "application/vnd.github.v3+json");
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return (null, false);

            var json = await response.Content.ReadAsStringAsync();
            var release = JObject.Parse(json);

            var tagName = release["tag_name"]?.Value<string>() ?? string.Empty;
            var nieuwVersie = tagName.TrimStart('v');
            if (string.IsNullOrEmpty(nieuwVersie)) return (null, false);

            if (Version.TryParse(nieuwVersie, out var vNieuw) &&
                Version.TryParse(HuidigeVersie(), out var vHuidige) &&
                vNieuw > vHuidige)
            {
                // Find the release assets:
                //   - framework-dependent zip  → name ends with -win-x64.zip (not -standalone)
                //   - installer                → name ends with -Setup.exe
                var assets = release["assets"] as JArray;
                var zipAsset = assets?.FirstOrDefault(a =>
                {
                    var name = a["name"]?.Value<string>() ?? string.Empty;
                    return name.EndsWith("-win-x64.zip", StringComparison.OrdinalIgnoreCase)
                        && !name.Contains("-standalone", StringComparison.OrdinalIgnoreCase);
                });
                var installerAsset = assets?.FirstOrDefault(a =>
                {
                    var name = a["name"]?.Value<string>() ?? string.Empty;
                    return name.EndsWith("-Setup.exe", StringComparison.OrdinalIgnoreCase);
                });

                return (new UpdateInfo
                {
                    VersieNummer = nieuwVersie,
                    ReleaseNotes = release["body"]?.Value<string>() ?? string.Empty,
                    DownloadUrl = release["html_url"]?.Value<string>() ?? string.Empty,
                    ZipDownloadUrl = zipAsset?["browser_download_url"]?.Value<string>() ?? string.Empty,
                    InstallerDownloadUrl = installerAsset?["browser_download_url"]?.Value<string>() ?? string.Empty,
                    PublicatieDatum = release["published_at"]?.Value<string>() ?? string.Empty,
                }, true);
            }

            return (null, true);
        }
        catch
        {
            return (null, false);
        }
    }
}

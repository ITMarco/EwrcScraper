using EwrcScraper.Models;
using EwrcScraper.Services;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Windows;

namespace EwrcScraper.Views;

public partial class UpdateDialog : Window
{
    private readonly UpdateInfo _update;
    private readonly bool _isInstalled;

    public UpdateDialog(UpdateInfo update)
    {
        InitializeComponent();
        _update = update;
        _isInstalled = UpdateService.IsInstalledVersion();

        TitelTekst.Text = $"EWRC Scraper v{update.VersieNummer} is beschikbaar";
        if (update.PublicatieDatum.Length >= 10)
            DatumTekst.Text = $"Gepubliceerd op {update.PublicatieDatum[..10]}";
        NotesTekst.Text = string.IsNullOrWhiteSpace(update.ReleaseNotes)
            ? "Geen release-notities beschikbaar."
            : update.ReleaseNotes;

        // The auto-update button uses the installer for installed copies and the
        // portable zip otherwise. Hide it if the matching asset is missing.
        if (_isInstalled)
        {
            AutoUpdateBtn.Content = "⬆ Update installeren";
            if (string.IsNullOrEmpty(update.InstallerDownloadUrl))
                AutoUpdateBtn.Visibility = Visibility.Collapsed;
        }
        else
        {
            AutoUpdateBtn.Content = "⬆ Auto-update";
            if (string.IsNullOrEmpty(update.ZipDownloadUrl))
                AutoUpdateBtn.Visibility = Visibility.Collapsed;
        }
    }

    private void Download_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_update.DownloadUrl))
            Process.Start(new ProcessStartInfo(_update.DownloadUrl) { UseShellExecute = true });
        Close();
    }

    private void Later_Click(object sender, RoutedEventArgs e) => Close();

    private async void AutoUpdate_Click(object sender, RoutedEventArgs e)
    {
        AutoUpdateBtn.IsEnabled = false;
        DownloadBtn.IsEnabled = false;
        LaterBtn.IsEnabled = false;
        VoortgangPanel.Visibility = Visibility.Visible;

        try
        {
            if (_isInstalled)
                await RunInstallerUpdateAsync();
            else
                await RunPortableUpdateAsync();
        }
        catch (Exception ex)
        {
            Voortgang.IsIndeterminate = false;
            VoortgangPanel.Visibility = Visibility.Collapsed;
            AutoUpdateBtn.IsEnabled = true;
            DownloadBtn.IsEnabled = true;
            LaterBtn.IsEnabled = true;
            MessageBox.Show(
                $"Update mislukt:\n{ex.Message}\n\nGebruik 'Zelf downloaden' om handmatig bij te werken.",
                "Update mislukt", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // Installed copy (Program Files): download the Setup.exe, launch it, quit so the
    // installer can replace the locked files. The installer relaunches the app afterwards.
    private async Task RunInstallerUpdateAsync()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "EwrcScraperUpdate");
        Directory.CreateDirectory(tempDir);
        var installerPath = Path.Combine(tempDir, $"EwrcScraper-v{_update.VersieNummer}-Setup.exe");

        if (string.IsNullOrEmpty(_update.InstallerDownloadUrl))
            throw new InvalidOperationException("Geen installer-URL beschikbaar in de release. Gebruik 'Zelf downloaden'.");

        await DownloadToFileAsync(_update.InstallerDownloadUrl, installerPath);

        if (!File.Exists(installerPath) || new FileInfo(installerPath).Length < 1024)
            throw new InvalidOperationException("Gedownload bestand is leeg of ontbreekt.");

        // Remove the "downloaded from internet" mark so SmartScreen doesn't silently block it
        try { File.Delete(installerPath + ":Zone.Identifier"); } catch { }

        VoortgangTekst.Text = "Installatieprogramma starten...";
        var proc = Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true,   // required so the installer's admin manifest triggers UAC
        });

        if (proc == null)
            throw new InvalidOperationException("Kon het installatieprogramma niet starten. Mogelijk is UAC uitgeschakeld of het bestand geblokkeerd.");

        // Only shut down once the installer process is confirmed running.
        Application.Current.Shutdown();
    }

    // Portable copy: download the framework-dependent zip, extract the exe, then a
    // PowerShell helper waits for exit, swaps the exe in place and restarts.
    private async Task RunPortableUpdateAsync()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "EwrcScraperUpdate");
        Directory.CreateDirectory(tempDir);
        var zipPath = Path.Combine(tempDir, $"EwrcScraper-v{_update.VersieNummer}.zip");
        var newExePath = Path.Combine(tempDir, "EwrcScraper_new.exe");

        await DownloadToFileAsync(_update.ZipDownloadUrl, zipPath);

        VoortgangTekst.Text = "Uitpakken...";
        Voortgang.IsIndeterminate = true;
        using (var zip = ZipFile.OpenRead(zipPath))
        {
            var entry = zip.GetEntry("EwrcScraper.exe")
                ?? throw new InvalidOperationException("EwrcScraper.exe niet gevonden in zip.");
            entry.ExtractToFile(newExePath, overwrite: true);
        }
        File.Delete(zipPath);

        var huidigExe = Process.GetCurrentProcess().MainModule!.FileName;
        var scriptPath = Path.Combine(Path.GetTempPath(), "EwrcScraperUpdater.ps1");
        File.WriteAllText(scriptPath, $"""
            Start-Sleep -Seconds 2
            Copy-Item -Path "{newExePath}" -Destination "{huidigExe}" -Force
            Start-Process -FilePath "{huidigExe}"
            Remove-Item -Path "{newExePath}" -Force -ErrorAction SilentlyContinue
            Remove-Item -Path "{scriptPath}" -Force -ErrorAction SilentlyContinue
            """);

        VoortgangTekst.Text = "Installeren en herstarten...";
        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            UseShellExecute = false,
        });

        Application.Current.Shutdown();
    }

    // Streams a download to disk with progress reporting on the dialog.
    private async Task DownloadToFileAsync(string url, string destPath)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("User-Agent", "EwrcScraper/1.0 (RCH)");
        http.Timeout = TimeSpan.FromMinutes(5);

        VoortgangTekst.Text = "Verbinding maken...";
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        using var src = await response.Content.ReadAsStreamAsync();
        using var dst = File.Create(destPath);

        var buffer = new byte[81920];
        long downloaded = 0;
        int read;
        while ((read = await src.ReadAsync(buffer)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, read));
            downloaded += read;
            if (totalBytes > 0)
            {
                Voortgang.Value = (double)downloaded / totalBytes * 100;
                VoortgangTekst.Text =
                    $"Downloaden... {downloaded / 1048576.0:F1} / {totalBytes / 1048576.0:F1} MB";
            }
        }
    }
}

using EwrcScraper.Models;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Windows;

namespace EwrcScraper.Views;

public partial class UpdateDialog : Window
{
    private readonly UpdateInfo _update;

    public UpdateDialog(UpdateInfo update)
    {
        InitializeComponent();
        _update = update;
        TitelTekst.Text = $"EWRC Scraper v{update.VersieNummer} is beschikbaar";
        if (update.PublicatieDatum.Length >= 10)
            DatumTekst.Text = $"Gepubliceerd op {update.PublicatieDatum[..10]}";
        NotesTekst.Text = string.IsNullOrWhiteSpace(update.ReleaseNotes)
            ? "Geen release-notities beschikbaar."
            : update.ReleaseNotes;

        // Hide auto-update button when no direct zip is available
        if (string.IsNullOrEmpty(update.ZipDownloadUrl))
            AutoUpdateBtn.Visibility = Visibility.Collapsed;
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
            var tempDir = Path.Combine(Path.GetTempPath(), "EwrcScraperUpdate");
            Directory.CreateDirectory(tempDir);
            var zipPath = Path.Combine(tempDir, $"EwrcScraper-v{_update.VersieNummer}.zip");
            var newExePath = Path.Combine(tempDir, "EwrcScraper_new.exe");

            // Download
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "EwrcScraper/1.0 (RCH)");
            http.Timeout = TimeSpan.FromMinutes(5);

            VoortgangTekst.Text = "Verbinding maken...";
            using var response = await http.GetAsync(_update.ZipDownloadUrl,
                HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            using (var src = await response.Content.ReadAsStreamAsync())
            using (var dst = File.Create(zipPath))
            {
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

            // Extract exe
            VoortgangTekst.Text = "Uitpakken...";
            Voortgang.IsIndeterminate = true;
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                var entry = zip.GetEntry("EwrcScraper.exe")
                    ?? throw new InvalidOperationException("EwrcScraper.exe niet gevonden in zip.");
                entry.ExtractToFile(newExePath, overwrite: true);
            }
            File.Delete(zipPath);

            // Write PowerShell updater script
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
        catch (Exception ex)
        {
            Voortgang.IsIndeterminate = false;
            VoortgangPanel.Visibility = Visibility.Collapsed;
            AutoUpdateBtn.IsEnabled = true;
            DownloadBtn.IsEnabled = true;
            LaterBtn.IsEnabled = true;
            MessageBox.Show(
                $"Auto-update mislukt:\n{ex.Message}\n\nGebruik 'Zelf downloaden' om handmatig bij te werken.",
                "Update mislukt", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

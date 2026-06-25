using EwrcScraper.Models;
using System.Diagnostics;
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
    }

    private void Download_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_update.DownloadUrl))
            Process.Start(new ProcessStartInfo(_update.DownloadUrl) { UseShellExecute = true });
        Close();
    }

    private void Later_Click(object sender, RoutedEventArgs e) => Close();
}

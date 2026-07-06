using EwrcScraper.Models;
using EwrcScraper.Services;
using System.Windows;

namespace EwrcScraper.Views;

public partial class OccasionSettingsWindow : Window
{
    private readonly OccasionService _service;

    public OccasionSettingsWindow(OccasionService service)
    {
        InitializeComponent();
        _service = service;

        var s = _service.LoadSettings();
        StartMijlpalenBox.Text = string.Join(", ", s.StartMijlpalen);
        KmMijlpalenBox.Text = string.Join(", ", s.KilometerMijlpalen);
        JubileumBox.Text = string.Join(", ", s.JubileumJaren);
        LeeftijdBox.Text = string.Join(", ", s.LeeftijdMijlpalen);
        StartVoorBox.Text = s.StartVooraankondiging.ToString();
        KmVoorBox.Text = s.KilometerVooraankondiging.ToString();
        VerjaardagVoorBox.Text = s.VerjaardagVooraankondigingDagen.ToString();
        InactiefBox.Text = s.InactiefNaJaren.ToString();
    }

    private void Opslaan_Click(object sender, RoutedEventArgs e)
    {
        var settings = new OccasionSettings
        {
            StartMijlpalen = ParseLijst(StartMijlpalenBox.Text),
            KilometerMijlpalen = ParseLijst(KmMijlpalenBox.Text),
            JubileumJaren = ParseLijst(JubileumBox.Text),
            LeeftijdMijlpalen = ParseLijst(LeeftijdBox.Text),
            StartVooraankondiging = ParseGetal(StartVoorBox.Text, 1),
            KilometerVooraankondiging = ParseGetal(KmVoorBox.Text, 500),
            VerjaardagVooraankondigingDagen = ParseGetal(VerjaardagVoorBox.Text, 14),
            InactiefNaJaren = ParseGetal(InactiefBox.Text, 3),
        };

        _service.SaveSettings(settings);
        DialogResult = true;
        Close();
    }

    private void Annuleren_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    // Parses "10, 25, 50" into a sorted, de-duplicated list of positive ints.
    private static List<int> ParseLijst(string text) =>
        text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => int.TryParse(t.Trim(), out var n) ? n : -1)
            .Where(n => n > 0)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

    private static int ParseGetal(string text, int standaard) =>
        int.TryParse(text.Trim(), out var n) && n >= 0 ? n : standaard;
}

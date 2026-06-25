using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EwrcScraper.Models;
using EwrcScraper.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;

namespace EwrcScraper.ViewModels;

public partial class ComparisonViewModel : ObservableObject
{
    private readonly EwrcApiService _api;
    private readonly DebugService _debug;

    private List<DriverEntry> _alleInschrijvingen = new();

    public ObservableCollection<RallyMatch> Matches { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GefilterdeMatches))]
    private string _filter = string.Empty;

    [ObservableProperty]
    private bool _isBezig;

    [ObservableProperty]
    private string _statusTekst = string.Empty;

    [ObservableProperty]
    private string _voortgangTekst = string.Empty;

    [ObservableProperty]
    private int _voortgang;

    [ObservableProperty]
    private int _voortgangMax = 100;

    public IEnumerable<RallyMatch> GefilterdeMatches =>
        string.IsNullOrWhiteSpace(Filter)
            ? Matches
            : Matches.Where(m =>
                m.VolledigeNaam.Contains(Filter, StringComparison.OrdinalIgnoreCase) ||
                m.Rally.Contains(Filter, StringComparison.OrdinalIgnoreCase) ||
                m.EmailAdres.Contains(Filter, StringComparison.OrdinalIgnoreCase));

    public ComparisonViewModel(EwrcApiService api, DebugService debug)
    {
        _api = api;
        _debug = debug;
    }

    public async Task HaalRallyInfoOp(IEnumerable<RallyEvent> geselecteerdeEvents)
    {
        var events = geselecteerdeEvents.ToList();
        if (events.Count == 0)
        {
            StatusTekst = "Geen rally's geselecteerd in Stap 1.";
            return;
        }

        IsBezig = true;
        _alleInschrijvingen.Clear();
        Matches.Clear();
        VoortgangMax = events.Count;
        Voortgang = 0;

        try
        {
            foreach (var ev in events)
            {
                VoortgangTekst = $"Ophalen: {ev.Name}...";
                try
                {
                    var entries = await _api.GetEntriesAsync(ev.Id, ev.Name);
                    _alleInschrijvingen.AddRange(entries);
                }
                catch (Exception ex)
                {
                    _debug.Log($"Fout bij {ev.Name}: {ex.Message}");
                }
                Voortgang++;
            }

            StatusTekst = $"{_alleInschrijvingen.Count} inschrijvingen geladen uit {events.Count} rally's.";
            VoortgangTekst = string.Empty;
        }
        finally
        {
            IsBezig = false;
        }
    }

    public void Vergelijk(List<RchMember> leden)
    {
        if (_alleInschrijvingen.Count == 0)
        {
            StatusTekst = "Haal eerst rally-inschrijvingen op (knop hierboven).";
            return;
        }
        if (leden.Count == 0)
        {
            StatusTekst = "Laad eerst de ledenlijst in Stap 2.";
            return;
        }

        Matches.Clear();

        foreach (var inschrijving in _alleInschrijvingen)
        {
            var lid = leden.FirstOrDefault(l =>
                (!string.IsNullOrEmpty(l.EwrcNrPilot) && l.EwrcNrPilot == inschrijving.Number) ||
                (!string.IsNullOrEmpty(l.EwrcNrCoPilot) && l.EwrcNrCoPilot == inschrijving.Number));

            if (lid != null)
            {
                Matches.Add(new RallyMatch
                {
                    LedenNr = lid.LedenNr,
                    VolledigeNaam = lid.VolledigeNaam,
                    EmailAdres = lid.EmailAdres,
                    EwrcNr = inschrijving.Number,
                    Rol = inschrijving.Type == "Driver" ? "Rijder" : "Bijrijder",
                    Rally = inschrijving.RallyName,
                });
            }
        }

        OnPropertyChanged(nameof(GefilterdeMatches));
        StatusTekst = $"{Matches.Count} RCH leden gevonden in de geselecteerde rally's.";
        _debug.Log($"Vergelijking klaar: {Matches.Count} matches.");
    }

    [RelayCommand]
    private void KopieerEmails()
    {
        var emails = Matches.Where(m => !string.IsNullOrEmpty(m.EmailAdres))
                            .Select(m => m.EmailAdres)
                            .Distinct()
                            .ToList();
        if (emails.Count == 0) { StatusTekst = "Geen e-mailadressen om te kopiëren."; return; }
        Clipboard.SetText(string.Join(";", emails));
        StatusTekst = $"{emails.Count} e-mailadressen gekopieerd naar klembord.";
    }

    [RelayCommand]
    private void ExporteerCsv()
    {
        if (Matches.Count == 0) { StatusTekst = "Geen resultaten om te exporteren."; return; }

        var dialog = new SaveFileDialog
        {
            Title = "Exporteer resultaten",
            Filter = "CSV bestanden|*.csv",
            FileName = $"RCH_rally_matches_{DateTime.Now:yyyyMMdd}.csv"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var lines = new List<string>
            {
                "Leden Nr.;Naam;E-mail;EWRC Nr.;Rol;Rally"
            };
            foreach (var m in Matches)
                lines.Add($"{m.LedenNr};{m.VolledigeNaam};{m.EmailAdres};{m.EwrcNr};{m.Rol};{m.Rally}");

            File.WriteAllLines(dialog.FileName, lines, System.Text.Encoding.UTF8);
            StatusTekst = $"Geëxporteerd naar {Path.GetFileName(dialog.FileName)}.";
        }
        catch (Exception ex)
        {
            StatusTekst = $"Export mislukt: {ex.Message}";
        }
    }

    partial void OnFilterChanged(string value) => OnPropertyChanged(nameof(GefilterdeMatches));
}

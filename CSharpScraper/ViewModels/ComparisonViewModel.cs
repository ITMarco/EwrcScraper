using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EwrcScraper.Models;
using EwrcScraper.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;

namespace EwrcScraper.ViewModels;

public partial class ComparisonViewModel : ObservableObject
{
    private readonly EwrcApiService _api;
    private readonly DebugService _debug;

    // All entries fetched — public so grid can bind to it
    public ObservableCollection<DriverEntry> AlleInschrijvingen { get; } = new();

    public ObservableCollection<RallyMatch> Matches { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GefilterdeMatches))]
    private string _filter = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GefilterdeInschrijvingen))]
    private string _filterInschrijvingen = string.Empty;

    [ObservableProperty]
    private bool _toonInschrijvingenRaster;

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

    public IEnumerable<DriverEntry> GefilterdeInschrijvingen =>
        string.IsNullOrWhiteSpace(FilterInschrijvingen)
            ? AlleInschrijvingen
            : AlleInschrijvingen.Where(e =>
                e.Name.Contains(FilterInschrijvingen, StringComparison.OrdinalIgnoreCase) ||
                e.RallyName.Contains(FilterInschrijvingen, StringComparison.OrdinalIgnoreCase));

    public string InschrijvingenSamenvatting =>
        AlleInschrijvingen.Count == 0 ? string.Empty :
        $"{AlleInschrijvingen.Count} deelnemers uit {AlleInschrijvingen.Select(e => e.RallyName).Distinct().Count()} rally('s)";

    public ComparisonViewModel(EwrcApiService api, DebugService debug)
    {
        _api = api;
        _debug = debug;
    }

    public async Task HaalRallyInfoOp(IEnumerable<RallyEvent> geselecteerdeEvents)
    {
        try
        {
            var events = geselecteerdeEvents.ToList();
            _debug.Log($"Rally info ophalen: {events.Count} rally('s) geselecteerd: {string.Join(", ", events.Select(e => e.Name))}");

            if (events.Count == 0)
            {
                StatusTekst = "Geen rally's geselecteerd in Stap 1.";
                return;
            }

            ToonInschrijvingenRaster = false;
            IsBezig = true;
            AlleInschrijvingen.Clear();
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
                        foreach (var entry in entries)
                        {
                            entry.RallyDate = ev.From;
                            AlleInschrijvingen.Add(entry);
                        }
                    }
                    catch (Exception ex)
                    {
                        _debug.Log($"Fout bij {ev.Name}: {ex.Message}");
                    }
                    Voortgang++;
                }

                StatusTekst = $"{AlleInschrijvingen.Count} deelnemers geladen uit {events.Count} rally's.";
                VoortgangTekst = string.Empty;
                OnPropertyChanged(nameof(InschrijvingenSamenvatting));
                ToonInschrijvingenRaster = AlleInschrijvingen.Count > 0;
            }
            finally
            {
                IsBezig = false;
            }
        }
        catch (Exception ex)
        {
            _debug.Log($"Onverwachte fout in HaalRallyInfoOp: {ex.GetType().Name}: {ex.Message}");
            StatusTekst = $"Fout: {ex.Message}";
            IsBezig = false;
        }
    }

    [RelayCommand]
    private void TerugNaarRallySelectie() => ToonInschrijvingenRaster = false;

    public void Vergelijk(List<RchMember> leden)
    {
        if (AlleInschrijvingen.Count == 0)
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

        foreach (var inschrijving in AlleInschrijvingen)
        {
            var lid = leden.FirstOrDefault(l =>
                (!string.IsNullOrEmpty(l.EwrcNrPilot)   && l.EwrcNrPilot   == inschrijving.Number) ||
                (!string.IsNullOrEmpty(l.EwrcNrCoPilot) && l.EwrcNrCoPilot == inschrijving.Number));

            if (lid != null)
            {
                Matches.Add(new RallyMatch
                {
                    LedenNr      = lid.LedenNr,
                    VolledigeNaam = lid.VolledigeNaam,
                    EmailAdres   = lid.EmailAdres,
                    EwrcNr       = inschrijving.Number,
                    Rol          = inschrijving.TypeNl,
                    Rally        = inschrijving.RallyName,
                    RallyDatum   = inschrijving.RallyDate,
                });
            }
        }

        OnPropertyChanged(nameof(GefilterdeMatches));
        StatusTekst = $"{Matches.Count} RCH leden gevonden in de geselecteerde rally's.";
        _debug.Log($"Vergelijking klaar: {Matches.Count} matches.");
    }

    // ── Inschrijvingen export ──────────────────────────────────────────────

    [RelayCommand]
    private void KopieerInschrijvingen()
    {
        if (AlleInschrijvingen.Count == 0) { StatusTekst = "Geen deelnemers om te kopiëren."; return; }
        var sb = new StringBuilder();
        sb.AppendLine("Naam\tRol\tEWRC Nr.\tRally\tDatum");
        foreach (var e in AlleInschrijvingen)
            sb.AppendLine($"{e.Name}\t{e.TypeNl}\t{e.Number}\t{e.RallyName}\t{e.RallyDate}");
        Clipboard.SetText(sb.ToString());
        StatusTekst = $"{AlleInschrijvingen.Count} rijen gekopieerd naar klembord.";
    }

    [RelayCommand]
    private void ExporteerInschrijvingenCsv()
    {
        if (AlleInschrijvingen.Count == 0) { StatusTekst = "Geen deelnemers om te exporteren."; return; }

        var dialog = new SaveFileDialog
        {
            Title = "Exporteer deelnemers naar CSV",
            Filter = "CSV bestanden|*.csv",
            FileName = $"Rally_deelnemers_{DateTime.Now:yyyyMMdd}.csv"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var lines = new List<string> { "Naam;Rol;EWRC Nr.;Rally;Datum" };
            foreach (var e in AlleInschrijvingen)
                lines.Add($"{e.Name};{e.TypeNl};{e.Number};{e.RallyName};{e.RallyDate}");
            File.WriteAllLines(dialog.FileName, lines, Encoding.UTF8);
            StatusTekst = $"Geëxporteerd naar {Path.GetFileName(dialog.FileName)}.";
        }
        catch (Exception ex) { StatusTekst = $"Export mislukt: {ex.Message}"; }
    }

    [RelayCommand]
    private void ExporteerInschrijvingenExcel()
    {
        if (AlleInschrijvingen.Count == 0) { StatusTekst = "Geen deelnemers om te exporteren."; return; }

        var dialog = new SaveFileDialog
        {
            Title = "Exporteer deelnemers naar Excel",
            Filter = "Excel bestanden|*.xlsx",
            FileName = $"Rally_deelnemers_{DateTime.Now:yyyyMMdd}.xlsx"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Deelnemers");

            ws.Cell(1, 1).Value = "Naam";
            ws.Cell(1, 2).Value = "Rol";
            ws.Cell(1, 3).Value = "EWRC Nr.";
            ws.Cell(1, 4).Value = "Rally";
            ws.Cell(1, 5).Value = "Datum";
            ws.Row(1).Style.Font.Bold = true;
            ws.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1A3A5C");
            ws.Row(1).Style.Font.FontColor = XLColor.White;

            int row = 2;
            foreach (var e in AlleInschrijvingen)
            {
                ws.Cell(row, 1).Value = e.Name;
                ws.Cell(row, 2).Value = e.TypeNl;
                ws.Cell(row, 3).Value = e.Number;
                ws.Cell(row, 4).Value = e.RallyName;
                ws.Cell(row, 5).Value = e.RallyDate;
                row++;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(dialog.FileName);
            StatusTekst = $"Geëxporteerd naar {Path.GetFileName(dialog.FileName)}.";
        }
        catch (Exception ex) { StatusTekst = $"Export mislukt: {ex.Message}"; }
    }

    // ── Matches export ────────────────────────────────────────────────────

    [RelayCommand]
    private void KopieerEmails()
    {
        var emails = Matches.Where(m => !string.IsNullOrEmpty(m.EmailAdres))
                            .Select(m => m.EmailAdres).Distinct().ToList();
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
            var lines = new List<string> { "Leden Nr.;Naam;E-mail;EWRC Nr.;Rol;Rally" };
            foreach (var m in Matches)
                lines.Add($"{m.LedenNr};{m.VolledigeNaam};{m.EmailAdres};{m.EwrcNr};{m.Rol};{m.Rally}");
            File.WriteAllLines(dialog.FileName, lines, Encoding.UTF8);
            StatusTekst = $"Geëxporteerd naar {Path.GetFileName(dialog.FileName)}.";
        }
        catch (Exception ex) { StatusTekst = $"Export mislukt: {ex.Message}"; }
    }

    partial void OnFilterChanged(string value) => OnPropertyChanged(nameof(GefilterdeMatches));
    partial void OnFilterInschrijvingenChanged(string value) => OnPropertyChanged(nameof(GefilterdeInschrijvingen));
}

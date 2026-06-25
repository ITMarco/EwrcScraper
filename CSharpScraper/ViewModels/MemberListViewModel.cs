using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EwrcScraper.Models;
using EwrcScraper.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;

namespace EwrcScraper.ViewModels;

public partial class MemberListViewModel : ObservableObject
{
    private readonly MemberListService _service;
    private readonly DebugService _debug;
    private readonly PreferencesService _prefs;

    [ObservableProperty]
    private string _ledenlijstPad = string.Empty;

    [ObservableProperty]
    private string _statusTekst = string.Empty;

    [ObservableProperty]
    private int _aantalLeden;

    public ObservableCollection<RchMember> LedenGrid { get; } = new();
    public List<RchMember> Leden { get; private set; } = new();

    public MemberListViewModel(MemberListService service, DebugService debug, PreferencesService prefs)
    {
        _service = service;
        _debug = debug;
        _prefs = prefs;
    }

    public void LaadVanPad(string pad)
    {
        if (string.IsNullOrEmpty(pad) || !File.Exists(pad)) return;
        LedenlijstPad = pad;
        LaadBestand();
    }

    [RelayCommand]
    private void BrowseLedenlijst()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Ledenlijst openen",
            Filter = "Ledenlijst bestanden|*.csv;*.xlsx;*.xls|CSV bestanden|*.csv|Excel bestanden|*.xlsx;*.xls|Alle bestanden|*.*",
            InitialDirectory = string.IsNullOrEmpty(LedenlijstPad)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : Path.GetDirectoryName(LedenlijstPad)
        };

        if (dialog.ShowDialog() == true)
        {
            LedenlijstPad = dialog.FileName;
            LaadBestand();
        }
    }

    private void LaadBestand()
    {
        if (string.IsNullOrEmpty(LedenlijstPad)) return;
        try
        {
            Leden = _service.Load(LedenlijstPad);
            AantalLeden = Leden.Count;
            StatusTekst = $"{AantalLeden} leden geladen uit {Path.GetFileName(LedenlijstPad)}.";

            LedenGrid.Clear();
            foreach (var lid in Leden)
                LedenGrid.Add(lid);

            _prefs.SaveLedenlijstPad(LedenlijstPad);
        }
        catch (Exception ex)
        {
            StatusTekst = $"Fout bij laden: {ex.Message}";
            _debug.Log($"Ledenlijst laadfout: {ex.Message}");
        }
    }
}

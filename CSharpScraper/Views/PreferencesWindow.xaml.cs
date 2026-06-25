using EwrcScraper.Services;
using EwrcScraper.ViewModels;
using System.Windows;

namespace EwrcScraper.Views;

public partial class PreferencesWindow : Window
{
    private readonly PreferencesViewModel _vm;
    private readonly PreferencesService _prefs;

    public PreferencesWindow(PreferencesViewModel vm, PreferencesService prefs)
    {
        InitializeComponent();
        _vm = vm;
        _prefs = prefs;
        DataContext = vm;
    }

    private async void CheckNu_Click(object sender, RoutedEventArgs e)
    {
        _vm.IsUpdateAanHetControleren = true;
        _vm.UpdateStatusTekst = "Controleren...";

        var update = await _vm.UpdateService.CheckForUpdateAsync();

        _vm.IsUpdateAanHetControleren = false;

        if (update != null)
        {
            _vm.UpdateStatusTekst = $"Nieuwe versie beschikbaar: v{update.VersieNummer}";
            var dialog = new UpdateDialog(update);
            dialog.Owner = this;
            dialog.ShowDialog();
        }
        else
        {
            _vm.UpdateStatusTekst = "✔ Je hebt de nieuwste versie.";
        }
    }

    private void Opslaan_Click(object sender, RoutedEventArgs e)
    {
        var prefs = _prefs.Load();
        _vm.ToepassenOp(prefs);
        _prefs.Save(prefs);
        DialogResult = true;
        Close();
    }

    private void Annuleer_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

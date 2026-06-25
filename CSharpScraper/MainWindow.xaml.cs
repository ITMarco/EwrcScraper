using EwrcScraper.Models;
using EwrcScraper.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace EwrcScraper;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private Views.DebugWindow? _debugWindow;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        var prefs = _vm.Voorkeuren;
        Left = prefs.WindowX;
        Top = prefs.WindowY;
        Width = prefs.WindowWidth;
        Height = prefs.WindowHeight;

        await _vm.InitialiserenAsync();

        if (_vm.Voorkeuren.DebugVensterZichtbaar)
            OpenDebugWindow();

        if (_vm.BeschikbareUpdate != null)
        {
            var dialog = new Views.UpdateDialog(_vm.BeschikbareUpdate);
            dialog.Owner = this;
            dialog.ShowDialog();
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _vm.VoorkeurenOpslaan(Left, Top, Width, Height);
        _debugWindow?.Close();
        base.OnClosing(e);
    }

    private void OpenDebugWindow()
    {
        if (_debugWindow == null || !_debugWindow.IsVisible)
        {
            _debugWindow = new Views.DebugWindow(_vm.Debug);
            _debugWindow.Owner = this;
            _debugWindow.Show();
        }
        else
        {
            _debugWindow.Activate();
        }
    }

    private void BtnDebug_Click(object sender, RoutedEventArgs e) => OpenDebugWindow();

    private void BtnVoorkeuren_Click(object sender, RoutedEventArgs e)
    {
        var huidigePrefs = _vm.VoorkeurenService.Load();
        var vm = new PreferencesViewModel(huidigePrefs, _vm.UpdateService);
        var dialog = new Views.PreferencesWindow(vm, _vm.VoorkeurenService);
        dialog.Owner = this;
        dialog.ShowDialog();
    }

    private void JaarTerug_Click(object sender, RoutedEventArgs e)
        => _vm.RallySelectie.GeselecteerdJaar--;

    private void JaarVooruit_Click(object sender, RoutedEventArgs e)
        => _vm.RallySelectie.GeselecteerdJaar++;

    private void LedenlijstGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var hit = e.OriginalSource as DependencyObject;

        var colHeader = FindVisualParent<DataGridColumnHeader>(hit);
        if (colHeader?.Column != null)
        {
            var col = colHeader.Column;
            var colName = col.Header?.ToString() ?? "kolom";
            var menu = new ContextMenu();
            var item = new MenuItem { Header = $"📋 Kopieer kolom '{colName}'" };
            item.Click += (_, _) =>
            {
                var idx = LedenlijstGrid.Columns.IndexOf(col);
                var values = LedenlijstGrid.Items.OfType<RchMember>().Select(m => idx switch
                {
                    0 => m.LedenNr,
                    1 => m.Voornaam,
                    2 => m.Achternaam,
                    3 => m.EwrcNrPilot,
                    4 => m.EwrcNrCoPilot,
                    5 => m.EmailAdres,
                    _ => string.Empty
                });
                Clipboard.SetText(string.Join(Environment.NewLine, values));
            };
            menu.Items.Add(item);
            menu.PlacementTarget = LedenlijstGrid;
            menu.IsOpen = true;
            e.Handled = true;
            return;
        }

        var row = FindVisualParent<DataGridRow>(hit);
        if (row?.Item is RchMember member)
        {
            LedenlijstGrid.SelectedItem = member;
            var menu = new ContextMenu();
            var item = new MenuItem { Header = "📋 Kopieer rij" };
            item.Click += (_, _) =>
            {
                var text = string.Join("\t", new[]
                {
                    member.LedenNr, member.Voornaam, member.Achternaam,
                    member.EwrcNrPilot, member.EwrcNrCoPilot, member.EmailAdres
                });
                Clipboard.SetText(text);
            };
            menu.Items.Add(item);
            menu.PlacementTarget = LedenlijstGrid;
            menu.IsOpen = true;
            e.Handled = true;
        }
    }

    private void InschrijvingenGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var hit = e.OriginalSource as DependencyObject;

        var colHeader = FindVisualParent<DataGridColumnHeader>(hit);
        if (colHeader?.Column != null)
        {
            var col = colHeader.Column;
            var menu = new ContextMenu();
            var item = new MenuItem { Header = $"📋 Kopieer kolom '{col.Header}'" };
            item.Click += (_, _) =>
            {
                var idx = InschrijvingenGrid.Columns.IndexOf(col);
                var values = InschrijvingenGrid.Items.OfType<DriverEntry>().Select(e => idx switch
                {
                    0 => e.Name,
                    1 => e.TypeNl,
                    2 => e.Number,
                    3 => e.RallyName,
                    4 => e.RallyDate,
                    _ => string.Empty
                });
                Clipboard.SetText(string.Join(Environment.NewLine, values));
            };
            menu.Items.Add(item);
            menu.PlacementTarget = InschrijvingenGrid;
            menu.IsOpen = true;
            e.Handled = true;
            return;
        }

        var row = FindVisualParent<DataGridRow>(hit);
        if (row?.Item is DriverEntry clickedEntry)
        {
            if (!InschrijvingenGrid.SelectedItems.Contains(clickedEntry))
                InschrijvingenGrid.SelectedItem = clickedEntry;

            var selected = InschrijvingenGrid.SelectedItems.OfType<DriverEntry>().ToList();
            var menu = new ContextMenu();
            var label = selected.Count > 1 ? $"📋 Kopieer {selected.Count} geselecteerde rijen" : "📋 Kopieer rij";
            var copyItem = new MenuItem { Header = label };
            copyItem.Click += (_, _) =>
            {
                var lines = selected.Select(e => string.Join("\t",
                    new[] { e.Name, e.TypeNl, e.Number, e.RallyName, e.RallyDate }));
                Clipboard.SetText(string.Join(Environment.NewLine, lines));
            };
            menu.Items.Add(copyItem);
            menu.PlacementTarget = InschrijvingenGrid;
            menu.IsOpen = true;
            e.Handled = true;
        }
    }

    private void VergelijkGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var hit = e.OriginalSource as DependencyObject;

        var colHeader = FindVisualParent<DataGridColumnHeader>(hit);
        if (colHeader?.Column != null)
        {
            var col = colHeader.Column;
            var colName = col.Header?.ToString() ?? "kolom";
            var menu = new ContextMenu();
            var item = new MenuItem { Header = $"📋 Kopieer kolom '{colName}'" };
            item.Click += (_, _) =>
            {
                var idx = VergelijkGrid.Columns.IndexOf(col);
                var values = VergelijkGrid.Items.OfType<RallyMatch>().Select(m => idx switch
                {
                    0 => m.LedenNr,
                    1 => m.VolledigeNaam,
                    2 => m.EmailAdres,
                    3 => m.EwrcNr,
                    4 => m.Rol,
                    5 => m.Rally,
                    _ => string.Empty
                });
                Clipboard.SetText(string.Join(Environment.NewLine, values));
            };
            menu.Items.Add(item);
            menu.PlacementTarget = VergelijkGrid;
            menu.IsOpen = true;
            e.Handled = true;
            return;
        }

        var row = FindVisualParent<DataGridRow>(hit);
        if (row?.Item is RallyMatch clickedMatch)
        {
            if (!VergelijkGrid.SelectedItems.Contains(clickedMatch))
                VergelijkGrid.SelectedItem = clickedMatch;

            var selected = VergelijkGrid.SelectedItems.OfType<RallyMatch>().ToList();
            var menu = new ContextMenu();
            var label = selected.Count > 1 ? $"📋 Kopieer {selected.Count} geselecteerde rijen" : "📋 Kopieer rij";
            var copyRows = new MenuItem { Header = label };
            copyRows.Click += (_, _) =>
            {
                var lines = selected.Select(m => string.Join("\t", new[]
                    { m.LedenNr, m.VolledigeNaam, m.EmailAdres, m.EwrcNr, m.Rol, m.Rally }));
                Clipboard.SetText(string.Join(Environment.NewLine, lines));
            };
            menu.Items.Add(copyRows);

            if (selected.Count > 0)
            {
                var copyEmails = new MenuItem { Header = "📧 Kopieer e-mailadressen" };
                copyEmails.Click += (_, _) =>
                {
                    var emails = selected
                        .Where(m => !string.IsNullOrEmpty(m.EmailAdres))
                        .Select(m => m.EmailAdres).Distinct();
                    Clipboard.SetText(string.Join(";", emails));
                };
                menu.Items.Add(copyEmails);
            }

            menu.PlacementTarget = VergelijkGrid;
            menu.IsOpen = true;
            e.Handled = true;
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T match) return match;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }
}

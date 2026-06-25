using EwrcScraper.ViewModels;
using System.Windows;

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
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _vm.VoorkeurenOpslaan(Left, Top, Width, Height);
        _debugWindow?.Close();
        base.OnClosing(e);
    }

    private void BtnDebug_Click(object sender, RoutedEventArgs e)
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

    private void JaarTerug_Click(object sender, RoutedEventArgs e)
        => _vm.RallySelectie.GeselecteerdJaar--;

    private void JaarVooruit_Click(object sender, RoutedEventArgs e)
        => _vm.RallySelectie.GeselecteerdJaar++;
}

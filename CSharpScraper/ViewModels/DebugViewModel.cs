using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EwrcScraper.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace EwrcScraper.ViewModels;

public partial class DebugViewModel : ObservableObject
{
    private readonly DebugService _debug;
    private readonly PreferencesService _prefs;

    public ObservableCollection<string> LogRegels { get; } = new();

    public DebugViewModel(DebugService debug, PreferencesService prefs)
    {
        _debug = debug;
        _prefs = prefs;
        _debug.LogAdded += OnLogAdded;

        foreach (var regel in _debug.GetAll())
            LogRegels.Add(regel);
    }

    private void OnLogAdded(string regel)
    {
        Application.Current.Dispatcher.Invoke(() => LogRegels.Add(regel));
    }

    [RelayCommand]
    private void Wissen()
    {
        _debug.Clear();
        LogRegels.Clear();
    }

    [RelayCommand]
    private void KopieerNaarKlembord()
    {
        Clipboard.SetText(string.Join(Environment.NewLine, LogRegels));
    }

    [RelayCommand]
    private void ToonVoorkeuren()
    {
        try
        {
            var pad = PreferencesService.ConfigPath;
            string inhoud;
            if (File.Exists(pad))
                inhoud = File.ReadAllText(pad);
            else
                inhoud = "(Geen voorkeuren-bestand gevonden — wordt aangemaakt bij afsluiten.)";

            var window = new Window
            {
                Title = "Voorkeuren — " + pad,
                Width = 600,
                Height = 400,
                WindowStyle = WindowStyle.ToolWindow,
                Background = System.Windows.Media.Brushes.White
            };
            var textBox = new System.Windows.Controls.TextBox
            {
                Text = inhoud,
                IsReadOnly = true,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12,
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                Margin = new Thickness(8)
            };
            window.Content = textBox;
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fout bij openen voorkeuren:\n{ex.Message}", "Fout",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EwrcScraper.Services;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace EwrcScraper.ViewModels;

public partial class DebugViewModel : ObservableObject
{
    private readonly DebugService _debug;
    private readonly PreferencesService _prefs;

    // Incoming log lines can arrive from any thread (HTTP continuations run off the UI
    // thread) and in bursts. They are queued here and flushed into the bound collection
    // in batches on the UI thread by a low-priority timer, so the ListBox is never
    // mutated mid-layout — the cause of the "ItemsControl is inconsistent with its
    // items source" crash.
    private readonly ConcurrentQueue<string> _binnenkomend = new();
    private readonly DispatcherTimer _flushTimer;
    private const int MaxRegels = 2000;

    public ObservableCollection<string> LogRegels { get; } = new();

    public DebugViewModel(DebugService debug, PreferencesService prefs)
    {
        _debug = debug;
        _prefs = prefs;
        _debug.LogAdded += OnLogAdded;

        foreach (var regel in _debug.GetAll())
            LogRegels.Add(regel);

        _flushTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _flushTimer.Tick += (_, _) => Flush();
        _flushTimer.Start();
    }

    private void OnLogAdded(string regel) => _binnenkomend.Enqueue(regel);

    // Runs on the UI thread (DispatcherTimer) at Background priority: it never fires
    // during a layout/measure pass, so adding here is safe.
    private void Flush()
    {
        bool gewijzigd = false;
        while (_binnenkomend.TryDequeue(out var regel))
        {
            LogRegels.Add(regel);
            gewijzigd = true;
        }
        if (!gewijzigd) return;

        // Keep the list bounded so a long session stays responsive.
        while (LogRegels.Count > MaxRegels)
            LogRegels.RemoveAt(0);
    }

    [RelayCommand]
    private void Wissen()
    {
        while (_binnenkomend.TryDequeue(out _)) { }
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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EwrcScraper.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace EwrcScraper.ViewModels;

public partial class DebugViewModel : ObservableObject
{
    private readonly DebugService _debug;

    public ObservableCollection<string> LogRegels { get; } = new();

    public DebugViewModel(DebugService debug)
    {
        _debug = debug;
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
}

using EwrcScraper.ViewModels;
using System.Collections.Specialized;
using System.Windows;

namespace EwrcScraper.Views;

public partial class DebugWindow : Window
{
    public DebugWindow(DebugViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.LogRegels.CollectionChanged += ScrollToBottom;
    }

    private void ScrollToBottom(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (LogList.Items.Count > 0)
            LogList.ScrollIntoView(LogList.Items[^1]);
    }
}

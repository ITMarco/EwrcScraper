using CommunityToolkit.Mvvm.ComponentModel;

namespace EwrcScraper.Models;

public partial class RallyEvent : ObservableObject
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Season { get; set; }
    public string From { get; set; } = string.Empty;
    public string Until { get; set; } = string.Empty;
    public int Days { get; set; }
    public string Country { get; set; } = string.Empty;
    public string Flag { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int Cancelled { get; set; }
    public string Url { get; set; } = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    public string DisplayName => $"{Flag} {Name} ({From})";
    public bool IsCancelled => Cancelled != 0;
}

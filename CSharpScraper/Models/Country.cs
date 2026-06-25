using CommunityToolkit.Mvvm.ComponentModel;

namespace EwrcScraper.Models;

public partial class Country : ObservableObject
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Flag { get; set; } = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    public string DisplayName => $"{Flag} {Name}";
}

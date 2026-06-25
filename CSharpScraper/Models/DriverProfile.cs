namespace EwrcScraper.Models;

public class DriverProfile
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Born { get; set; } = string.Empty;
    public string PhotoUrl { get; set; } = string.Empty;
    public List<DriverCategoryStats> Stats { get; set; } = new();
}

public class DriverCategoryStats
{
    public string Category { get; set; } = string.Empty;
    public int Starts { get; set; }
    public int Wins { get; set; }
    public int Podiums { get; set; }
    public int Finishes { get; set; }
}

public class SearchResult
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Extra { get; set; } = string.Empty;
}

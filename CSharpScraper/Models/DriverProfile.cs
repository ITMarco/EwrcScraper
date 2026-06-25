namespace EwrcScraper.Models;

public class DriverProfile
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Born { get; set; } = string.Empty;
    public string PhotoUrl { get; set; } = string.Empty;
    public string ProfileUrl { get; set; } = string.Empty;
    public List<DriverCategoryStats> Stats { get; set; } = new();
}

public class DriverCategoryStats
{
    public string Category { get; set; } = string.Empty;
    public int Starts { get; set; }
    public int Wins { get; set; }
    public int KlasseWinst { get; set; }
    public int Uitval { get; set; }
    public double UitvalPct { get; set; }
    public string UitvalDisplay => UitvalPct > 0 ? $"{Uitval} ({UitvalPct:F0}%)" : $"{Uitval}";
}

public class VergelijkRegel
{
    public string EwrcId { get; set; } = string.Empty;
    public string Naam { get; set; } = string.Empty;
    public string Land { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Geboren { get; set; } = string.Empty;
    public int Starts { get; set; }
    public string Uitval { get; set; } = string.Empty;
    public int Winst { get; set; }
    public int KlasseWinst { get; set; }
}

public class SearchResult
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Extra { get; set; } = string.Empty;
}

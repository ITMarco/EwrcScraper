namespace EwrcScraper.Models;

public class RallyMatch
{
    public string LedenNr { get; set; } = string.Empty;
    public int LedenNrSort => int.TryParse(LedenNr, out var n) ? n : int.MaxValue;
    public string VolledigeNaam { get; set; } = string.Empty;
    public string EmailAdres { get; set; } = string.Empty;
    public string EwrcNr { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public string Rally { get; set; } = string.Empty;
    public string RallyDatum { get; set; } = string.Empty;
}

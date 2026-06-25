namespace EwrcScraper.Models;

public class DriverEntry
{
    public string Name { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string RallyName { get; set; } = string.Empty;
    public int RallyId { get; set; }
    public string RallyDate { get; set; } = string.Empty;

    public int NumberSort => int.TryParse(Number, out var n) ? n : int.MaxValue;
    public string TypeNl => Type == "Driver" ? "Rijder" : "Bijrijder";
}

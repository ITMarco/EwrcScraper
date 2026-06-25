namespace EwrcScraper.Models;

public class RchMember
{
    public string LedenNr { get; set; } = string.Empty;
    public string Voornaam { get; set; } = string.Empty;
    public string Achternaam { get; set; } = string.Empty;
    public string EwrcNrPilot { get; set; } = string.Empty;
    public string EwrcNrCoPilot { get; set; } = string.Empty;
    public string EmailAdres { get; set; } = string.Empty;

    public string VolledigeNaam => $"{Voornaam} {Achternaam}".Trim();

    public Dictionary<string, string> ExtraVelden { get; set; } = new();
}

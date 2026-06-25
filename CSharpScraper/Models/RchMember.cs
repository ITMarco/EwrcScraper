namespace EwrcScraper.Models;

public class RchMember
{
    public string LedenNr { get; set; } = string.Empty;
    public string Voornaam { get; set; } = string.Empty;
    public string Tussenvoegsel { get; set; } = string.Empty;
    public string Achternaam { get; set; } = string.Empty;
    public string EwrcNrPilot { get; set; } = string.Empty;
    public string EwrcNrCoPilot { get; set; } = string.Empty;
    public string EmailAdres { get; set; } = string.Empty;

    public string VolledigeNaam => string.IsNullOrEmpty(Tussenvoegsel)
        ? $"{Voornaam} {Achternaam}".Trim()
        : $"{Voornaam} {Tussenvoegsel} {Achternaam}".Trim();

    public Dictionary<string, string> ExtraVelden { get; set; } = new();
}

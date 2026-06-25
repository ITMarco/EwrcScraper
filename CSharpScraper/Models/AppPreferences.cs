namespace EwrcScraper.Models;

public class AppPreferences
{
    public double WindowX { get; set; } = 100;
    public double WindowY { get; set; } = 100;
    public double WindowWidth { get; set; } = 1000;
    public double WindowHeight { get; set; } = 700;

    public List<int> GeselecteerdeCountryIds { get; set; } = new();
    public string LedenlijstPad { get; set; } = string.Empty;
    public bool DebugVensterZichtbaar { get; set; } = false;
    public bool ControleerUpdates { get; set; } = true;
    public string ApiBaseUrl { get; set; } = "https://api-next.ewrc-results.com";
}

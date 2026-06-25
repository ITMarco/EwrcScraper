namespace EwrcScraper.Models;

public class UpdateInfo
{
    public string VersieNummer { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string ZipDownloadUrl { get; set; } = string.Empty;       // framework-dependent zip (portable update)
    public string InstallerDownloadUrl { get; set; } = string.Empty; // Setup.exe (installed update)
    public string PublicatieDatum { get; set; } = string.Empty;
}

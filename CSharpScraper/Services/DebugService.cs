namespace EwrcScraper.Services;

public class DebugService
{
    private readonly List<string> _log = new();

    public event Action<string>? LogAdded;

    public void Log(string message)
    {
        var stamped = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _log.Add(stamped);
        LogAdded?.Invoke(stamped);
    }

    public IReadOnlyList<string> GetAll() => _log.AsReadOnly();

    public void Clear() => _log.Clear();
}

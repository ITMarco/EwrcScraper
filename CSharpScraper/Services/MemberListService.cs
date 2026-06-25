using ClosedXML.Excel;
using EwrcScraper.Models;

namespace EwrcScraper.Services;

public class MemberListService
{
    private readonly DebugService _debug;

    public MemberListService(DebugService debug)
    {
        _debug = debug;
    }

    public List<RchMember> Load(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        _debug.Log($"Ledenlijst laden: {Path.GetFileName(filePath)}");

        return ext switch
        {
            ".csv" => LoadCsv(filePath),
            ".xlsx" or ".xls" => LoadExcel(filePath),
            _ => throw new NotSupportedException($"Bestandstype '{ext}' wordt niet ondersteund. Gebruik .csv of .xlsx.")
        };
    }

    private List<RchMember> LoadCsv(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length < 2) return new List<RchMember>();

        var sep = DetectSeparator(lines[0]);
        var headers = lines[0].Split(sep).Select(h => h.Trim().Trim('"')).ToArray();

        var members = new List<RchMember>();
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var cols = SplitCsvLine(lines[i], sep);
            members.Add(MapColumns(headers, cols));
        }

        _debug.Log($"{members.Count} leden geladen uit CSV.");
        return members;
    }

    private List<RchMember> LoadExcel(string path)
    {
        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        var rows = ws.RangeUsed()?.RowsUsed().ToList();
        if (rows == null || rows.Count < 2) return new List<RchMember>();

        var headers = rows[0].Cells().Select(c => c.Value.ToString().Trim()).ToArray();
        var members = new List<RchMember>();

        for (int i = 1; i < rows.Count; i++)
        {
            var cols = rows[i].CellsUsed().Select(c => c.Value.ToString()).ToArray();
            members.Add(MapColumns(headers, cols));
        }

        _debug.Log($"{members.Count} leden geladen uit Excel.");
        return members;
    }

    private static RchMember MapColumns(string[] headers, string[] values)
    {
        var member = new RchMember();
        for (int i = 0; i < headers.Length && i < values.Length; i++)
        {
            var h = headers[i].ToLowerInvariant().Replace(" ", "").Replace(".", "").Replace("-", "");
            var v = values[i].Trim().Trim('"');
            switch (h)
            {
                case "ledennr": member.LedenNr = v; break;
                case "voornaam": member.Voornaam = v; break;
                case "achternaam": member.Achternaam = v; break;
                case "ewrcnrpilot": member.EwrcNrPilot = v; break;
                case "ewrcnrcopilot": member.EwrcNrCoPilot = v; break;
                case "emailadres" or "email" or "eMailadres": member.EmailAdres = v; break;
                default: member.ExtraVelden[headers[i]] = v; break;
            }
        }
        return member;
    }

    private static char DetectSeparator(string header)
    {
        if (header.Contains(';')) return ';';
        if (header.Contains('\t')) return '\t';
        return ',';
    }

    private static string[] SplitCsvLine(string line, char sep)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();
        foreach (char c in line)
        {
            if (c == '"') { inQuotes = !inQuotes; continue; }
            if (c == sep && !inQuotes) { result.Add(current.ToString()); current.Clear(); continue; }
            current.Append(c);
        }
        result.Add(current.ToString());
        return result.ToArray();
    }
}

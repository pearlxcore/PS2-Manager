using System.Text;
using ClosedXML.Excel;
using PS2Manager.Core.Models;

namespace PS2Manager.Core.Export;

public sealed record ExportColumn(string Header, Func<GameEntry, object?> Value, bool DefaultOn = true);

public static class LibraryExporter
{
    public static IReadOnlyList<ExportColumn> Columns { get; } =
    [
        new("Title", g => g.Title),
        new("Game ID", g => g.HasGameId ? g.GameId : ""),
        new("Region", g => g.Region),
        new("Media", g => g.DiscType.ToString()),
        new("Disc", g => g.DiscNumber),
        new("Version", g => g.Version),
        new("Genre", g => g.Genre),
        new("Developer", g => g.Developer),
        new("Publisher", g => g.Publisher),
        new("Release", g => g.Release),
        new("Size", g => g.SizeDisplay),
        new("Size (bytes)", g => g.SizeBytes, false),
        new("PCSX2 CRC", g => g.Pcsx2Crc?.ToString("X8") ?? ""),
        new("CRC32", g => g.Crc32?.ToString("X8") ?? "", false),
        new("Status", g => g.FileMissing ? "Missing" : g.Validity.ToString()),
        new("File Name", g => g.FileName),
        new("Folder", g => g.Directory),
    ];

    public static void Export(IEnumerable<GameEntry> games, IReadOnlyList<ExportColumn> columns, string path)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".xlsx": ExportExcel(games, columns, path); break;
            case ".csv": ExportCsv(games, columns, path); break;
            default: ExportText(games, columns, path); break;
        }
    }

    private static void ExportExcel(IEnumerable<GameEntry> games, IReadOnlyList<ExportColumn> columns, string path)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("PS2 Library");
        for (int c = 0; c < columns.Count; c++) ws.Cell(1, c + 1).Value = columns[c].Header;

        int row = 2;
        foreach (var g in games)
        {
            for (int c = 0; c < columns.Count; c++)
            {
                var cell = ws.Cell(row, c + 1);
                switch (columns[c].Value(g))
                {
                    case int i: cell.Value = i; break;
                    case long l: cell.Value = l; break;
                    case var v: cell.Value = v?.ToString() ?? ""; break;
                }
            }
            row++;
        }

        var header = ws.Range(1, 1, 1, columns.Count);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromArgb(0x2B, 0x2B, 0x2B);
        header.Style.Font.FontColor = XLColor.White;
        ws.SheetView.FreezeRows(1);
        if (row > 2) ws.Range(1, 1, row - 1, columns.Count).SetAutoFilter();
        ws.Columns(1, columns.Count).AdjustToContents(1, Math.Min(row, 500));
        wb.SaveAs(path);
    }

    private static void ExportCsv(IEnumerable<GameEntry> games, IReadOnlyList<ExportColumn> columns, string path)
    {
        static string Esc(object? v)
        {
            string s = v?.ToString() ?? "";
            return s.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", columns.Select(c => Esc(c.Header))));
        foreach (var g in games) sb.AppendLine(string.Join(",", columns.Select(c => Esc(c.Value(g)))));
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    private static void ExportText(IEnumerable<GameEntry> games, IReadOnlyList<ExportColumn> columns, string path)
    {
        int pad = columns.Max(c => c.Header.Length) + 1;
        var sb = new StringBuilder();
        foreach (var g in games)
        {
            foreach (var c in columns) sb.Append(c.Header.PadRight(pad)).Append(": ").AppendLine(c.Value(g)?.ToString());
            sb.AppendLine(new string('-', 60));
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }
}

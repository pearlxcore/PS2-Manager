using ClosedXML.Excel;

namespace PS2_ISO_Manager.Helper;

public static class Ps2ExcelExporter
{
    public static void Export(IEnumerable<Ps2IsoInfo> games, string outputPath)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("PS2 Library");

        // Headers
        string[] headers = { "File Name", "Title", "Game ID", "Region", "Disc", "Size", "CRC32", "Directory" };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromArgb(240, 240, 240);
        }

        // Data
        int row = 2;
        foreach (var g in games)
        {
            ws.Cell(row, 1).Value = g.FileName;
            ws.Cell(row, 2).Value = g.Title;
            ws.Cell(row, 3).Value = g.GameId;
            ws.Cell(row, 4).Value = g.Region;
            ws.Cell(row, 5).Value = g.DiscNumber;
            ws.Cell(row, 6).Value = g.SizeDisplay;
            ws.Cell(row, 7).Value = g.Pcsx2Crc?.ToString("X8") ?? "";
            ws.Cell(row, 8).Value = g.Directory;
            row++;
        }

        // Auto-fit columns
        ws.Columns().AdjustToContents(1, headers.Length);

        wb.SaveAs(outputPath);
    }
}

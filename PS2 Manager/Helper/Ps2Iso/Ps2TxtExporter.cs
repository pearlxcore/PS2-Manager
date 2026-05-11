using System.Collections.Generic;
using System.IO;
using System.Text;

public static class Ps2TxtExporter
{
    public static void Export(IEnumerable<Ps2IsoInfo> games, string outputPath)
    {
        var sb = new StringBuilder(64 * 1024);

        foreach (var g in games)
        {
            sb.AppendLine($"Title        : {g.Title}");
            sb.AppendLine($"Title Source : {g.TitleSource}");
            sb.AppendLine($"Game ID      : {g.GameId}");
            sb.AppendLine($"Region       : {g.Region}");
            sb.AppendLine($"Disc         : {g.DiscNumber}");
            sb.AppendLine($"Multi-Disc   : {(g.IsMultiDisc ? "Yes" : "No")}");
            sb.AppendLine($"Size         : {g.SizeDisplay}");
            sb.AppendLine($"CRC32        : {(g.Crc32.HasValue ? g.Crc32.Value.ToString("X8") : "N/A")}");
            sb.AppendLine($"Validity     : {g.Validity}");
            sb.AppendLine($"ISO Path     : {g.IsoPath}");
            sb.AppendLine($"Scanned At   : {g.ScannedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine(new string('-', 60));
        }

        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
    }
}

using System.Text;
using System.Text.RegularExpressions;
using DiscUtils.Iso9660;
using PS2Manager.Core.Models;

namespace PS2Manager.Core.Iso;

/// <summary>
/// Reads identifying information from a PS2 disc image (.iso / .bin / .img).
/// Handles 2048-byte (DVD/ISO) and raw 2352-byte (CD/BIN) sector layouts.
/// Pure disc reading — database lookup happens in MetadataResolver.
/// </summary>
public static partial class DiscReader
{
    public static readonly string[] SupportedExtensions = [".iso", ".bin", ".img"];

    /// <summary>Largest 2048-byte image we still treat as a CD when no UDF bridge is present (~80 min CD).</summary>
    private const long MaxCdBytes = 360_000L * 2048;

    public static bool IsSupported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static GameEntry Read(string path)
    {
        var fi = new FileInfo(path);
        var entry = new GameEntry
        {
            IsoPath = path,
            SizeBytes = fi.Exists ? fi.Length : 0,
            LastWriteUtc = fi.Exists ? fi.LastWriteTimeUtc : default,
            ScannedAt = DateTime.Now,
        };

        if (!fi.Exists)
        {
            entry.Validity = DiscValidity.Missing;
            return entry;
        }

        try
        {
            using var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var opened = Open(file);
            if (opened is null)
            {
                entry.Validity = DiscValidity.ReadError;
                return entry;
            }

            var (cd, isRaw, dataStream) = opened;
            entry.IsRawCdImage = isRaw;
            entry.DetectedDiscType = DetectDiscType(dataStream, isRaw, entry.SizeBytes);
            entry.DiscNumber = DetectDiscNumber(path);

            try { entry.VolumeLabel = cd.VolumeLabel?.Trim() ?? ""; } catch { /* optional */ }

            if (!cd.FileExists("SYSTEM.CNF"))
            {
                entry.Validity = DiscValidity.NoSystemCnf;
                return entry;
            }

            string cnf = ReadText(cd, "SYSTEM.CNF");
            entry.BootElf = ExtractBootElf(cnf);
            entry.Version = ExtractVersion(cnf);

            // Prefer the ID from BOOT2 (the executable name), then anything in SYSTEM.CNF.
            string id = GameIds.Extract(entry.BootElf);
            if (!GameIds.IsKnown(id)) id = GameIds.Extract(cnf);
            entry.GameId = id;

            if (!entry.HasGameId)
            {
                entry.Validity = DiscValidity.NoGameId;
                Log.Write($"No game ID in {path}: SYSTEM.CNF = {cnf.Trim()}");
                return entry;
            }

            entry.Pcsx2Crc = TryComputeElfCrc(cd, entry.BootElf);
            entry.Validity = DiscValidity.Valid;
        }
        catch (Exception ex)
        {
            Log.Error($"Reading {path}", ex);
            entry.Validity = DiscValidity.ReadError;
        }

        return entry;
    }

    /// <summary>Real layout from the file size: raw CD images are multiples of 2352 and not 2048.</summary>
    public static bool LooksLikeRawCd(long size) => size % 2352 == 0 && size % 2048 != 0;

    // ------------------------------------------------------------------

    private sealed class OpenedDisc(CDReader cd, bool isRaw, Stream dataStream, SectorStream? owned) : IDisposable
    {
        public void Deconstruct(out CDReader c, out bool raw, out Stream data) { c = cd; raw = isRaw; data = dataStream; }
        public void Dispose() { cd.Dispose(); owned?.Dispose(); }
    }

    private static OpenedDisc? Open(FileStream file)
    {
        // 1) plain 2048-byte ISO
        try { return new OpenedDisc(new CDReader(file, true), false, file, null); }
        catch { /* fall through */ }

        // 2) auto-detected raw CD, 3) forced raw CD for sizes divisible by both
        foreach (bool force in new[] { false, true })
        {
            if (force && file.Length % 2352 != 0) break;
            var ss = new SectorStream(file, forceCd: force);
            try { return new OpenedDisc(new CDReader(ss, true), ss.Length != file.Length, ss, ss); }
            catch { ss.Dispose(); }
        }
        return null;
    }

    /// <summary>
    /// PS2 DVDs are UDF/ISO9660 bridge discs; PS2 CDs are plain ISO9660.
    /// Look for a UDF volume recognition sequence (BEA01/NSR02/NSR03) after the ISO descriptors.
    /// </summary>
    internal static DiscType DetectDiscType(Stream data, bool isRaw, long fileSize)
    {
        if (isRaw) return DiscType.CD;
        try
        {
            var buf = new byte[6];
            for (int sector = 16; sector < 48; sector++)
            {
                data.Position = sector * 2048L;
                if (data.Read(buf, 0, 6) < 6) break;
                string ident = Encoding.ASCII.GetString(buf, 1, 5);
                if (ident is "BEA01" or "NSR02" or "NSR03") return DiscType.DVD;
                if (ident is "TEA01") break;
            }
        }
        catch { /* fall back to size */ }
        return fileSize > MaxCdBytes ? DiscType.DVD : DiscType.CD;
    }

    private static string ReadText(CDReader cd, string file)
    {
        using var s = cd.OpenFile(file, FileMode.Open);
        using var r = new StreamReader(s, Encoding.ASCII);
        return r.ReadToEnd();
    }

    internal static string ExtractBootElf(string cnf)
    {
        var m = BootRegex().Match(cnf);
        return m.Success ? m.Groups[1].Value.Trim() : "";
    }

    internal static string ExtractVersion(string cnf)
    {
        var m = VersionRegex().Match(cnf);
        return m.Success ? m.Groups[1].Value : "";
    }

    internal static int DetectDiscNumber(string path)
    {
        var m = DiscNumberRegex().Match(Path.GetFileNameWithoutExtension(path));
        if (!m.Success) m = DiscNumberRegex().Match(Path.GetFileName(Path.GetDirectoryName(path) ?? ""));
        return m.Success && int.TryParse(m.Groups[2].Value, out int n) && n is > 0 and < 10 ? n : 1;
    }

    /// <summary>Turns "GRAN_TURISMO4" into "Gran Turismo 4".</summary>
    public static string HumanizeVolumeLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return label;
        label = Regex.Replace(label, @"([A-Z])(\d)", "$1 $2");
        label = label.Replace('_', ' ').Replace('-', ' ');
        label = Regex.Replace(label.ToLowerInvariant(), @"\b\w", m => m.Value.ToUpperInvariant());
        return Regex.Replace(label, @"\s+", " ").Trim();
    }

    private static uint? TryComputeElfCrc(CDReader cd, string bootElf)
    {
        try
        {
            string elf = bootElf.Replace('\\', '/');
            int semi = elf.IndexOf(';');
            if (semi >= 0) elf = elf[..semi];
            elf = elf.TrimStart('/');

            foreach (var candidate in new[] { elf, elf + ";1", elf.ToUpperInvariant(), elf.ToUpperInvariant() + ";1" })
            {
                if (candidate.Length > 0 && cd.FileExists(candidate))
                    return Checksums.Pcsx2ElfCrc(cd, candidate);
            }

            string? largest = FindMainElf(cd);
            return largest is null ? null : Checksums.Pcsx2ElfCrc(cd, largest);
        }
        catch
        {
            return null;
        }
    }

    private static string? FindMainElf(CDReader cd)
    {
        string? best = null;
        long bestSize = 0;
        foreach (var file in cd.GetFiles("", "*", SearchOption.AllDirectories))
        {
            string name = file.ToUpperInvariant();
            if (!(name.EndsWith(".ELF") || name.EndsWith(".ELF;1") || ElfNameRegex().IsMatch(name)))
                continue;
            long len = cd.GetFileLength(file);
            if (len > bestSize) { bestSize = len; best = file; }
        }
        return best;
    }

    [GeneratedRegex(@"BOOT2\s*=\s*cdrom0?:\\?([^\r\n]+)", RegexOptions.IgnoreCase)]
    private static partial Regex BootRegex();

    [GeneratedRegex(@"VER\s*=\s*([0-9]+\.[0-9]+)", RegexOptions.IgnoreCase)]
    private static partial Regex VersionRegex();

    [GeneratedRegex(@"(disc|disk|cd)\s*[-_ ]?\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex DiscNumberRegex();

    [GeneratedRegex(@"S[LPCS][A-Z]{2}_[0-9]{3}\.[0-9]{2}")]
    private static partial Regex ElfNameRegex();
}

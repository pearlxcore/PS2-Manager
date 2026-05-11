using DiscUtils.Iso9660;
using PS2_ISO_Manager.Helper;
using PS2_ISO_Manager.Helper.Ps2Iso;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public sealed class Ps2IsoReader : IDisposable
{
    private readonly FileStream _stream;
    private readonly Stream _cdStream;
    private readonly CDReader _cd;
    private readonly Ps2GameDatabase _database;
    private bool _ownsStream;
    private bool _usedCdFallback;

    /// <summary>True if the file was read as CD format (2352 byte sectors) — extension may be wrong.</summary>
    public bool WasReadAsCd => _usedCdFallback;
    public string IsoPath { get; }

    public Ps2IsoReader(string isoPath, Ps2GameDatabase database)
    {
        if (!File.Exists(isoPath))
            throw new FileNotFoundException("Disc image not found", isoPath);

        _database = database ?? throw new ArgumentNullException(nameof(database));
        IsoPath = isoPath;

        _stream = File.Open(isoPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        try
        {
            _cd = new CDReader(_stream, true);
            _cdStream = _stream;
        }
        catch (Exception ex1)
        {
            // Try with sector conversion (handles CD-format: 2352 → 2048 bytes/sector)
            var sectorStream = new SectorStream(_stream);
            try
            {
                _cd = new CDReader(sectorStream, true);
                _cdStream = sectorStream;
                _ownsStream = true;
                _usedCdFallback = true;
            }
            catch
            {
                // Ambiguous size (divisible by both 2048 and 2352) — force CD mode
                if (_stream.Length % 2352 == 0)
                {
                    var forcedCd = new SectorStream(_stream, forceCd: true);
                    try
                    {
                        _cd = new CDReader(forcedCd, true);
                        _cdStream = forcedCd;
                        _ownsStream = true;
                        _usedCdFallback = true;
                        return;
                    }
                    catch (Exception ex3)
                    {
                        DebugLog.Write($"CDReader failed for {isoPath}: raw={ex1.Message}, cd={ex3.Message}");
                        throw;
                    }
                }
                DebugLog.Write($"CDReader failed for {isoPath}: {ex1.Message}");
                throw;
            }
        }
    }

    // ============================================================
    // Main Read Function (Returns Ps2IsoInfo)
    // ============================================================
    public Ps2IsoInfo Read(string isoPath)
    {
        var info = new Ps2IsoInfo
        {
            IsoPath = isoPath,
            SizeBytes = _stream.Length,
            ScannedAt = DateTime.Now
        };

        try
        {
            // Must exist or not a real PS2 ISO
            if (!_cd.FileExists("SYSTEM.CNF"))
            {
                info.Validity = Ps2IsoValidity.NoSystemCnf;
                return info;
            }

            string systemCnf = ReadSystemCnf();

            // Game ID extraction and cleanup
            string rawGameId = ExtractGameId(systemCnf);
            info.GameId = Ps2IdHelper.Normalize(rawGameId);

            if (info.GameId == "Unknown")
            {
                info.Validity = Ps2IsoValidity.NoGameId;
                DebugLog.Write($"NoGameId for {isoPath}: SYSTEM.CNF content: {systemCnf.Trim()}");
                return info;
            }

            // Basic Data
            info.Region = DetectRegion(info.GameId);
            info.BootElf = ExtractBootElf(systemCnf);
            info.Version = ExtractVersion(systemCnf);
            info.DiscNumber = DetectDiscNumber(isoPath);
            info.IsMultiDisc = info.DiscNumber > 1;

            // Title & metadata lookup
            info.Title = _database.GetTitle(info.GameId);
            info.TitleSource = info.Title != "Unknown"
                ? Ps2TitleSource.Database
                : Ps2TitleSource.Unknown;

            // Fallback: read volume label from ISO filesystem
            if (info.Title == "Unknown")
            {
                try
                {
                    string label = _cd.VolumeLabel;
                    if (!string.IsNullOrWhiteSpace(label))
                    {
                        info.Title = HumanizeVolumeLabel(label);
                        info.TitleSource = Ps2TitleSource.VolumeLabel;
                    }
                }
                catch { }
            }

            var metadata = _database.GetMetadata(info.GameId);
            if (metadata != null)
            {
                info.Region = metadata.Region ?? info.Region;
                info.Metadata = metadata;
            }

            // ==============================
            // 🔥 NEW — PCSX2 CRC
            // ==============================
            // ==============================
            // 🔥 PCSX2 CRC Detection
            // ==============================
            if (!string.IsNullOrWhiteSpace(info.BootElf))
            {
                try
                {
                    // Normalize ELF name from SYSTEM.CNF
                    string elf = info.BootElf.Trim()
                        .Replace("\\", "/")
                        .Replace("cdrom0:/", "", StringComparison.OrdinalIgnoreCase)
                        .Replace("cdrom0:\\", "", StringComparison.OrdinalIgnoreCase);

                    if (elf.Contains(";"))
                        elf = elf[..elf.IndexOf(";")]; // strip ";1"

                    // Try multiple filename formats
                    string[] tryPaths =
                    {
            elf,
            elf + ";1",
            "/" + elf,
            "/" + elf + ";1",
            elf.ToUpper(),
            elf.ToUpper() + ";1"
        };

                    foreach (var path in tryPaths)
                    {
                        if (_cd.FileExists(path))
                        {
                            info.Pcsx2Crc = Ps2CrcHelper.ComputeElfCrcPCSX2(_cd, path);
                            break;
                        }
                    }

                    // If still null → auto-find largest ELF inside ISO
                    if (info.Pcsx2Crc == null)
                    {
                        string bestElf = FindMainElf(_cd);
                        if (bestElf != null)
                            info.Pcsx2Crc = Ps2CrcHelper.ComputeElfCrcPCSX2(_cd, bestElf);
                    }
                }
                catch { info.Pcsx2Crc = null; }
            }



            info.Validity = Ps2IsoValidity.Valid;
        }
        catch
        {
            info.Validity = Ps2IsoValidity.ReadError;
        }

        return info;
    }

    // ============================================================
    // Internal readers/helpers
    // ============================================================

    private string ReadSystemCnf()
    {
        using var stream = _cd.OpenFile("SYSTEM.CNF", FileMode.Open);
        using var reader = new StreamReader(stream, Encoding.ASCII);
        return reader.ReadToEnd();
    }

	private static string ExtractGameId(string cnf)
	{
		// Standard: XXXX_123.45 / XXXX-12345 / XXXX_12345 etc
		// Matches all PS2 prefixes: SLUS, SCUS, SLES, SCES, SLPM, PBPX, TCPS, etc.
		var m = Regex.Match(cnf,
			@"[A-Z]{4}[-_ ]\d{3}\.\d{2}",
			RegexOptions.IgnoreCase);

		if (m.Success)
			return FixId(m.Value);

		// Fallback: XXXX-12345 / XXXX12345 (no dot)
		m = Regex.Match(cnf,
			@"[A-Z]{4}[-_ ]?\d{5}",
			RegexOptions.IgnoreCase);

		if (m.Success)
			return FixId(m.Value);

		return "Unknown";
	}


	private static string FixId(string raw)
	{
		string id = raw.ToUpper()
			.Replace("_", "")
			.Replace("-", "")
			.Replace(" ", "")
			.Replace(".", "")
			.Trim();

		// Convert to SCUS-97379 format
		if (id.Length > 4)
			id = id.Insert(4, "-");

		return id;
	}



	private static string ExtractBootElf(string cnf)
    {
        var match = Regex.Match(
            cnf,
            @"BOOT2\s*=\s*cdrom0:\\([^;]+)",
            RegexOptions.IgnoreCase);

        return match.Success ? match.Groups[1].Value : "";
    }

    internal static string HumanizeVolumeLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return label;

        // Insert space before digits that follow letters: POP2 → POP 2
        label = Regex.Replace(label, @"([A-Z])(\d)", "$1 $2");

        // Replace underscores and hyphens with spaces
        label = label.Replace('_', ' ').Replace('-', ' ');

        // Title case
        label = Regex.Replace(label.ToLower(), @"\b\w", m => m.Value.ToUpper());

        // Collapse whitespace
        label = Regex.Replace(label, @"\s+", " ").Trim();

        return label;
    }

    private static string ExtractVersion(string cnf)
    {
        var match = Regex.Match(cnf, @"VER\s*=\s*([0-9]+\.[0-9]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : "";
    }

    private static string DetectRegion(string gameId)
    {
        return gameId[..4] switch
        {
            // NTSC-U / North America
            "SLUS" or "SCUS" or "PBPX" => "NTSC-U",
            // PAL / Europe & Oceania
            "SLES" or "SCES" or "SLED" or "SCED" => "PAL",
            // NTSC-J / Japan & Korea
            "SLPM" or "SLPS" or "SCPS" or "SLKA" or "SCKA" => "NTSC-J",
            // Asia
            "SLAJ" or "SCAJ" or "SLAW" or "TCPS" => "NTSC-J",
            _ => "Unknown"
        };
    }

    private static int DetectDiscNumber(string path)
    {
        var match = Regex.Match(path, @"(disc|cd)\s*(\d+)", RegexOptions.IgnoreCase);
        return match.Success ? int.Parse(match.Groups[2].Value) : 1;
    }

    private string FindMainElf(CDReader cd)
    {
        string best = null;
        long bestSize = 0;

        foreach (var file in cd.GetFiles("", "*", SearchOption.AllDirectories))
        {
            string name = file.ToUpper();

            // Detect main ELF style files
            if (!(name.EndsWith(".ELF") ||
                  Regex.IsMatch(name, @"S[LPCS][A-Z]{2}_[0-9]{3}\.[0-9]{2}")))
                continue;

            using var s = cd.OpenFile(file, FileMode.Open);

            // Pick the largest ELF → main game executable
            if (s.Length > bestSize)
            {
                bestSize = s.Length;
                best = file;
            }
        }

        return best;
    }


    public void Dispose()
    {
        _cd.Dispose();
        if (_ownsStream)
            _cdStream.Dispose();
        _stream.Dispose();
    }
}

using System;
using System.ComponentModel;
using System.IO;

public enum Ps2TitleSource
{
    Database,
    Filename,
    VolumeLabel,
    Unknown
}

public enum Ps2IsoValidity
{
    Valid,
    NoSystemCnf,
    NoGameId,
    ReadError
}

public class Ps2IsoInfo
{
    [Browsable(false)]
    public long SizeBytes { get; set; }

    public string SizeDisplay
    {
        get
        {
            const double MB = 1024 * 1024;
            const double GB = 1024 * MB;

            if (SizeBytes >= GB)
                return $"{SizeBytes / GB:0.00} GB";

            return $"{SizeBytes / MB:0} MB";
        }
    }

    // File
    public string IsoPath { get; set; } = "";
    public string Directory => Path.GetDirectoryName(IsoPath) ?? "";

    public string FileName => Path.GetFileName(IsoPath);

    public Ps2GameMetadata? Metadata { get; set; }

    // PS2 Metadata
    public string GameId { get; set; } = "Unknown";
    public string Title { get; set; } = "Unknown";
    public Ps2TitleSource TitleSource { get; set; } = Ps2TitleSource.Unknown;
    public string Region { get; set; } = "Unknown";
    public string BootElf { get; set; } = "";
    public string Version { get; set; } = "";

    // Disc
    public int DiscNumber { get; set; } = 1;
    public bool IsMultiDisc { get; set; }

    // Integrity
    public uint? Crc32 { get; set; }
    public Ps2IsoValidity Validity { get; set; } = Ps2IsoValidity.Valid;

    // Library
    public DateTime ScannedAt { get; set; } = DateTime.Now;

    public bool IsValid => Validity == Ps2IsoValidity.Valid;
    public uint? Pcsx2Crc { get; set; }  // main CRC used by PCSX2

}

using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using PS2Manager.Core.Pcsx2;

namespace PS2Manager.Core.Models;

public enum TitleSource { Unknown, Override, Database, VolumeLabel, Filename }

public enum DiscValidity { Valid, NoSystemCnf, NoGameId, ReadError, Missing }

/// <summary>
/// One disc image in the library. Persisted fields are what was read from the
/// disc; <see cref="Info"/> is the merged (database + user override) view and
/// is recomputed by <see cref="Database.MetadataResolver"/>.
/// </summary>
public sealed partial class GameEntry : ObservableObject
{
    // ---------- File ----------
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileName), nameof(Directory), nameof(Extension), nameof(Title), nameof(OverrideKey))]
    private string _isoPath = "";

    public long SizeBytes { get; set; }
    public DateTime LastWriteUtc { get; set; }

    // ---------- Disc ----------
    public string GameId { get; set; } = GameIds.Unknown;
    public string VolumeLabel { get; set; } = "";
    public string BootElf { get; set; } = "";
    public string Version { get; set; } = "";
    public int DiscNumber { get; set; } = 1;
    public DiscType DetectedDiscType { get; set; }
    public bool IsRawCdImage { get; set; }
    [ObservableProperty] private uint? _pcsx2Crc;

    /// <summary>PCSX2 compatibility from its GameIndex.yaml (not persisted; filled when PCSX2 is found).</summary>
    [ObservableProperty][property: JsonIgnore] private Pcsx2Compat _pcsx2Compat;

    /// <summary>The background PCSX2 CRC pass tried this disc and could not read its ELF.</summary>
    public bool ElfCrcFailed { get; set; }

    [ObservableProperty] private uint? _crc32;
    [ObservableProperty] private DiscValidity _validity = DiscValidity.Valid;

    public DateTime ScannedAt { get; set; } = DateTime.Now;

    // ---------- Derived (not persisted) ----------
    [JsonIgnore] public string FileName => Path.GetFileName(IsoPath);
    [JsonIgnore] public string Directory => Path.GetDirectoryName(IsoPath) ?? "";
    [JsonIgnore] public string Extension => Path.GetExtension(IsoPath);
    [JsonIgnore] public bool HasGameId => GameIds.IsKnown(GameId);
    [JsonIgnore] public string OplId => GameIds.ToOplId(GameId);
    [JsonIgnore] public bool IsMultiDisc => DiscNumber > 1;
    [JsonIgnore] public bool IsValid => Validity == DiscValidity.Valid;

    [JsonIgnore]
    public string SizeDisplay => FormatSize(SizeBytes);

    public static string FormatSize(long bytes) => bytes switch
    {
        >= 1L << 40 => $"{bytes / (double)(1L << 40):0.00} TB",
        >= 1L << 30 => $"{bytes / (double)(1L << 30):0.00} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):0} MB",
        _ => $"{bytes / 1024.0:0} KB",
    };

    /// <summary>Key used for overrides: the game ID, or the path when the disc has none.</summary>
    [JsonIgnore]
    public string OverrideKey => HasGameId ? GameId : "path:" + IsoPath.ToLowerInvariant();

    [ObservableProperty]
    [property: JsonIgnore]
    [NotifyPropertyChangedFor(nameof(Title), nameof(Region), nameof(Genre), nameof(Developer),
        nameof(Publisher), nameof(Release), nameof(DiscType))]
    private GameInfo _info = new();

    [ObservableProperty][property: JsonIgnore] private TitleSource _titleSource;
    [ObservableProperty][property: JsonIgnore] private bool _hasOverrides;
    [ObservableProperty][property: JsonIgnore] private bool _inDatabase;
    [ObservableProperty][property: JsonIgnore] private string? _coverPath;
    [ObservableProperty][property: JsonIgnore] private bool _fileMissing;

    [JsonIgnore] public string Title => Info.Title ?? Path.GetFileNameWithoutExtension(IsoPath);
    [JsonIgnore] public string Region => Info.Region ?? GameIds.DetectRegion(GameId);
    [JsonIgnore] public string Genre => Info.Genre ?? "";
    [JsonIgnore] public string Developer => Info.Developer ?? "";
    [JsonIgnore] public string Publisher => Info.Publisher ?? "";
    [JsonIgnore] public string Release => Info.Release ?? "";
    [JsonIgnore] public DiscType DiscType => Info.Media ?? DetectedDiscType;

    [JsonIgnore]
    public string Year
    {
        get
        {
            var m = System.Text.RegularExpressions.Regex.Match(Release, @"(19|20)\d{2}");
            return m.Success ? m.Value : "";
        }
    }

    /// <summary>Call after mutating plain (non-observable) persisted fields.</summary>
    public void NotifyAllChanged() => OnPropertyChanged(string.Empty);
}

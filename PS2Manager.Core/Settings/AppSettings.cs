using PS2Manager.Core.Art;
using PS2Manager.Core.Opl;
using PS2Manager.Core.Pcsx2;
using PS2Manager.Core.Rename;

namespace PS2Manager.Core.Settings;

public sealed class AppSettings
{
    // Library
    public bool AutoScanOnStartup { get; set; } = true;
    public int ScanParallelism { get; set; } = 3;
    public string LibraryView { get; set; } = "Grid"; // Grid | Covers
    public double CoverSize { get; set; } = 150;
    public string LibrarySort { get; set; } = "Title";
    public bool ShowDetailsPane { get; set; } = true;
    public List<string> HiddenColumns { get; set; } = ["Disc"];
    public Dictionary<string, double> ColumnWidths { get; set; } = [];
    public List<string> Favorites { get; set; } = [];       // GameEntry.OverrideKey (game ID, or path for discs without one)
    public bool FavoritesFirst { get; set; } = true;

    // Database
    public bool CheckDatabaseOnStartup { get; set; } = true;

    // Art
    public List<string> ArtSourceOrder { get; set; } = [.. ArtSourceCatalog.DefaultOrder];
    public bool AutoDownloadCovers { get; set; } = true;
    public List<ArtType> BulkArtTypes { get; set; } = [ArtType.Cover];

    // Rename / organize
    public string RenameTemplate { get; set; } = RenameFormats.FilePresets[1].Template;
    public string FolderTemplate { get; set; } = RenameFormats.FolderPresets[0].Template;
    public ConflictMode ConflictMode { get; set; } = ConflictMode.Skip;

    // OPL
    public string? OplRootPath { get; set; }
    public TransferMode OplTransferMode { get; set; } = TransferMode.Copy;
    public string? OplRenameTemplate { get; set; } = "{TITLE}";
    public bool OplWriteCfg { get; set; } = true;
    public bool OplExportArt { get; set; } = true;
    public bool AutoWriteCfgOnSave { get; set; } = false;

    // PCSX2 (launch options only; everything else stays in PCSX2's own settings)
    public string? Pcsx2ExePath { get; set; }
    public Pcsx2Window Pcsx2Window { get; set; } = Pcsx2Window.Default;
    public Pcsx2Boot Pcsx2Boot { get; set; } = Pcsx2Boot.Default;
    public bool Pcsx2NoGui { get; set; }
    public bool Pcsx2BigPicture { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public Pcsx2LaunchOptions Pcsx2Options => new(Pcsx2Window, Pcsx2Boot, Pcsx2NoGui, Pcsx2BigPicture);

    // Window
    public double WindowWidth { get; set; } = 1400;
    public double WindowHeight { get; set; } = 860;
    public bool WindowMaximized { get; set; }

    public IReadOnlyList<IArtSource> ResolveArtSources() =>
        ArtSourceOrder.Select(ArtSourceCatalog.Find).OfType<IArtSource>().DefaultIfEmpty(ArtSourceCatalog.All[0]).ToList();

    public static AppSettings Load() => JsonFile.Load<AppSettings>(AppPaths.SettingsFile) ?? new AppSettings();

    public void Save()
    {
        try { JsonFile.Save(AppPaths.SettingsFile, this); }
        catch (Exception ex) { Log.Error("Saving settings", ex); }
    }
}

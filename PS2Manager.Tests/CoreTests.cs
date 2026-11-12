using System.Text;
using PS2Manager.Core.Art;
using PS2Manager.Core.Database;
using PS2Manager.Core.Iso;
using PS2Manager.Core.Library;
using PS2Manager.Core.Models;
using PS2Manager.Core.Opl;
using PS2Manager.Core.Organize;

namespace PS2Manager.Tests;

public class OplCfgTests
{
    private const string Sample =
        "CfgVersion=8\r\nTitle=Gran Turismo 4\r\n$Compatibility=5\r\nPlayers=players/2\r\n$VMC_0=GT4\r\nDescription=Real driving\r\n";

    [Fact]
    public void Parse_MapsKnownKeys_AndKeepsExtras()
    {
        var info = OplCfg.Parse(Sample).ToGameInfo();
        Assert.Equal("Gran Turismo 4", info.Title);
        Assert.Equal("players/2", info.Players);
        Assert.Equal(5, info.Compatibility);
        Assert.Equal("GT4", info.Extra!["$VMC_0"]);
        Assert.Equal("8", info.Extra["CfgVersion"]);
    }

    [Fact]
    public void Apply_PreservesOrderAndUnknownKeys()
    {
        var cfg = OplCfg.Parse(Sample);
        cfg.Apply(new GameInfo { Title = "GT4", Genre = "Racing", Compatibility = 0 });
        string text = cfg.ToString();
        Assert.StartsWith("CfgVersion=8\r\nTitle=GT4\r\n", text);
        Assert.Contains("$VMC_0=GT4", text);
        Assert.Contains("Genre=Racing", text);
        Assert.DoesNotContain("$Compatibility", text);   // 0 = no modes -> key removed
        Assert.DoesNotContain("Description", text);      // cleared field removed
    }

    [Fact]
    public void Apply_StripsNewlines()
    {
        var cfg = new OplCfg();
        cfg.Apply(new GameInfo { Description = "line1\nline2" });
        Assert.Equal("Description=line1 line2\r\n", cfg.ToString());
    }

    [Fact]
    public void Root_Paths_UseOplNaming()
    {
        var root = new OplRoot(@"E:\");
        Assert.Equal(@"E:\CFG\SLUS_200.62.cfg", root.CfgPath("SLUS-20062"));
        Assert.Equal(@"E:\ART\SLUS_200.62_COV2.png", root.ArtPath("SLUS-20062", ArtType.BackCover, ".png"));
    }

    [Fact]
    public void TargetPath_SortsByMedia_AndForcesIso()
    {
        var root = new OplRoot(@"E:\");
        var cd = TestDiscs.Entry(@"C:\g\a.img");
        cd.DetectedDiscType = DiscType.CD;
        var dvd = TestDiscs.Entry(@"C:\g\b.iso");
        dvd.DetectedDiscType = DiscType.DVD;
        Assert.Equal(@"E:\CD\Gran Turismo 4.iso", OplService.TargetPath(root, cd, "{TITLE}"));
        Assert.Equal(@"E:\DVD\b.iso", OplService.TargetPath(root, dvd, null));
    }
}

public class DatabaseTests
{
    private const string Json = """
    {
      "SLUS-20062": { "title": "Gran Turismo 4", "serial": "SLUS-20062", "region": "NTSC-U", "genre": ["Racing", "Simulation"], "developer": "Polyphony Digital" },
      "SCES-51719": { "title": "Gran Turismo 4 (EU)", "serial": ["SCES-51719", "SCES_517.19"], "release_date": 2005 }
    }
    """;

    private static GameDatabase Db() => GameDatabase.Parse(new MemoryStream(Encoding.UTF8.GetBytes(Json)));

    [Fact]
    public void Parse_HandlesArraysAndNumbers()
    {
        var db = Db();
        Assert.Equal("Racing, Simulation", db.Lookup("slus_200.62")!.Genre);
        Assert.Equal("2005", db.Lookup("SCES-51719")!.Release);
        Assert.Null(db.Lookup("SLUS-99999"));
    }

    [Fact]
    public void Resolver_OverrideWins_AndSurvivesDbValues()
    {
        using var tmp = new TempDir();
        var store = new OverrideStore(Path.Combine(tmp.Path, "o.json"));
        var resolver = new MetadataResolver(Db, store);
        var g = new GameEntry { IsoPath = @"C:\x.iso", GameId = "SLUS-20062" };

        resolver.Resolve(g);
        Assert.Equal("Gran Turismo 4", g.Title);
        Assert.Equal(TitleSource.Database, g.TitleSource);
        Assert.False(g.HasOverrides);

        store.Set(g.OverrideKey, new GameInfo { Title = "GT4 (edited)", Players = "players/2" });
        resolver.Resolve(g);
        Assert.Equal("GT4 (edited)", g.Title);
        Assert.Equal("Polyphony Digital", g.Developer);   // untouched DB field still shows
        Assert.Equal(TitleSource.Override, g.TitleSource);
        Assert.True(g.HasOverrides);

        // Persisted to disk
        Assert.Equal("GT4 (edited)", new OverrideStore(Path.Combine(tmp.Path, "o.json")).Get("SLUS-20062")!.Title);
    }

    [Fact]
    public void Resolver_FallsBackToVolumeLabel_ThenFilename()
    {
        using var tmp = new TempDir();
        var resolver = new MetadataResolver(Db, new OverrideStore(Path.Combine(tmp.Path, "o.json")));
        var labelled = new GameEntry { IsoPath = @"C:\a.iso", GameId = "SLUS-99999", VolumeLabel = "BURNOUT_3" };
        var bare = new GameEntry { IsoPath = @"C:\My Game.iso" };
        resolver.ResolveAll([labelled, bare]);
        Assert.Equal("Burnout 3", labelled.Title);
        Assert.Equal(TitleSource.VolumeLabel, labelled.TitleSource);
        Assert.Equal("My Game", bare.Title);
        Assert.Equal(TitleSource.Filename, bare.TitleSource);
    }

    [Fact]
    public void GameInfo_Merge_TopWins()
    {
        var merged = GameInfo.Merge(new GameInfo { Title = "A", Genre = "G" }, new GameInfo { Title = "B" });
        Assert.Equal("B", merged.Title);
        Assert.Equal("G", merged.Genre);
    }
}

public class DiscReaderTests
{
    [Fact]
    public void Reads_StandardIso()
    {
        using var tmp = new TempDir();
        string path = Path.Combine(tmp.Path, "Game (Disc 2).iso");
        File.WriteAllBytes(path, TestDiscs.BuildIso());

        var e = DiscReader.Read(path);
        Assert.Equal(DiscValidity.Valid, e.Validity);
        Assert.Equal("SLUS-20062", e.GameId);
        Assert.Equal("1.01", e.Version);
        Assert.Equal("GT_TEST", e.VolumeLabel);
        Assert.Equal(2, e.DiscNumber);
        Assert.False(e.IsRawCdImage);
        Assert.Equal(DiscType.CD, e.DetectedDiscType);   // tiny, no UDF bridge
        Assert.NotNull(e.Pcsx2Crc);
    }

    [Fact]
    public void Reads_RawCdImage()
    {
        using var tmp = new TempDir();
        string path = Path.Combine(tmp.Path, "cd.bin");
        File.WriteAllBytes(path, TestDiscs.ToRawCd(TestDiscs.BuildIso("SCES_500.00")));

        var e = DiscReader.Read(path);
        Assert.Equal(DiscValidity.Valid, e.Validity);
        Assert.Equal("SCES-50000", e.GameId);
        Assert.True(e.IsRawCdImage);
        Assert.Equal(DiscType.CD, e.DetectedDiscType);
    }

    [Fact]
    public void Garbage_IsReadError()
    {
        using var tmp = new TempDir();
        string path = Path.Combine(tmp.Path, "junk.iso");
        File.WriteAllBytes(path, new byte[2048 * 40]);
        Assert.Equal(DiscValidity.ReadError, DiscReader.Read(path).Validity);
    }

    [Fact]
    public void ElfCrc_IsXorOfWords()
    {
        var data = new byte[] { 1, 0, 0, 0, 2, 0, 0, 0, 0xFF };   // trailing partial word ignored
        Assert.Equal(3u, Checksums.Pcsx2ElfCrc(new MemoryStream(data)));
    }

    [Fact]
    public async Task Scanner_ReusesUnchangedEntries()
    {
        using var tmp = new TempDir();
        File.WriteAllBytes(Path.Combine(tmp.Path, "a.iso"), TestDiscs.BuildIso());
        var first = await LibraryScanner.ScanAsync([tmp.Path], [], fullRescan: false);
        Assert.Single(first.Games);
        Assert.Equal(1, first.NewCount);

        var second = await LibraryScanner.ScanAsync([tmp.Path], first.Games, fullRescan: false);
        Assert.Equal(1, second.ReusedCount);
        Assert.Same(first.Games[0], second.Games[0]);

        File.Delete(Path.Combine(tmp.Path, "a.iso"));
        var third = await LibraryScanner.ScanAsync([tmp.Path], second.Games, fullRescan: false);
        Assert.Empty(third.Games);
        Assert.Equal(1, third.RemovedCount);
    }
}

public class LibraryAndToolsTests
{
    [Fact]
    public void LegacyManifest_Imports()
    {
        using var tmp = new TempDir();
        string iso = tmp.File("g.iso");
        string manifest = tmp.File("ps2_manifest.json", $$"""
        { "SavedAt": "2025-01-01T00:00:00", "Games": [
          { "IsoPath": {{System.Text.Json.JsonSerializer.Serialize(iso)}}, "GameId": "SLUS-20062", "Title": "GT4", "TitleSource": 0, "Validity": 0, "DiscNumber": 1, "Pcsx2Crc": 305419896 },
          { "IsoPath": "C:\\nope\\x.iso", "GameId": "Unknown", "Title": "Burnout 3", "TitleSource": 2, "Validity": 2, "Crc32": null, "Pcsx2Crc": null, "Metadata": null }
        ]}
        """);

        var games = LibraryStore.ImportLegacyManifest(manifest);
        Assert.Equal(2, games.Count);
        Assert.Equal("SLUS-20062", games[0].GameId);
        Assert.Equal(0x12345678u, games[0].Pcsx2Crc);
        Assert.Equal("Burnout 3", games[1].VolumeLabel);
        Assert.Equal(DiscValidity.NoGameId, games[1].Validity);
    }

    [Fact]
    public void LibraryStore_RoundTrip()
    {
        using var tmp = new TempDir();
        string path = Path.Combine(tmp.Path, "lib.json");
        var g = TestDiscs.Entry(@"C:\g.iso");
        g.Crc32 = 0xDEADBEEF;
        LibraryStore.Save([@"C:\games"], [g], path);
        var loaded = LibraryStore.Load(path)!;
        Assert.Equal(@"C:\games", loaded.Folders.Single());
        Assert.Equal(0xDEADBEEFu, loaded.Games.Single().Crc32);
        Assert.Equal("SLUS-20062", loaded.Games.Single().GameId);
    }

    [Fact]
    public void Duplicates_ExactAndById()
    {
        var a = TestDiscs.Entry(@"C:\a.iso"); a.SizeBytes = 100;
        var b = TestDiscs.Entry(@"C:\dir\b.iso"); b.SizeBytes = 100;
        var c = TestDiscs.Entry(@"C:\c.iso"); c.SizeBytes = 200;
        Assert.Single(DuplicateFinder.Find([a, b, c], DuplicateMatch.Exact));
        var byId = DuplicateFinder.Find([a, b, c], DuplicateMatch.SameGameId).Single();
        Assert.Equal(3, byId.Games.Count);
        Assert.Equal(@"C:\a.iso", byId.Games[0].IsoPath);
    }

    [Fact]
    public void ArtStore_SaveDetectsFormat_AndReplaces()
    {
        using var tmp = new TempDir();
        var store = new ArtStore(Path.Combine(tmp.Path, "art"));
        var png = new byte[100]; png[0] = 0x89; png[1] = 0x50; png[2] = 0x4E; png[3] = 0x47;
        var jpg = new byte[100]; jpg[0] = 0xFF; jpg[1] = 0xD8; jpg[2] = 0xFF;

        string p1 = store.Save("SLUS-20062", ArtType.Cover, png, ".jpg");
        Assert.EndsWith("COV.png", p1);
        string p2 = store.Save("SLUS-20062", ArtType.Cover, jpg, ".png");
        Assert.EndsWith("COV.jpg", p2);
        Assert.False(File.Exists(p1));
        Assert.Equal(p2, store.Get("slus_200.62", ArtType.Cover));
    }
}

public class PerformanceBehaviourTests
{
    [Fact]
    public async Task Scan_SkipsElfCrc_ButKeepsEarlierValueOnFullRescan()
    {
        using var tmp = new TempDir();
        string iso = Path.Combine(tmp.Path, "a.iso");
        File.WriteAllBytes(iso, TestDiscs.BuildIso());

        var first = await LibraryScanner.ScanAsync([tmp.Path], [], fullRescan: false);
        var g = first.Games.Single();
        Assert.Null(g.Pcsx2Crc);                                   // not read during the scan
        Assert.Equal("SLUS-20062", g.GameId);

        g.Pcsx2Crc = DiscReader.ComputeElfCrc(iso, g.BootElf);       // what the background pass does
        Assert.NotNull(g.Pcsx2Crc);
        Assert.Equal(DiscReader.Read(iso).Pcsx2Crc, g.Pcsx2Crc);    // same result as a full read

        var full = await LibraryScanner.ScanAsync([tmp.Path], first.Games, fullRescan: true);
        Assert.NotSame(g, full.Games.Single());                      // disc was re-read…
        Assert.Equal(g.Pcsx2Crc, full.Games.Single().Pcsx2Crc);      // …but the CRC carried over
    }

    [Fact]
    public void ComputeElfCrc_MissingFile_ReturnsNull() =>
        Assert.Null(DiscReader.ComputeElfCrc(@"C:\definitely\not\here.iso", "SLUS_200.62;1"));

    private sealed class FakeSource(Func<ArtImage?> fetch) : IArtSource
    {
        public string Id => "fake";
        public string Name => "Fake";
        public string Description => "";
        public bool Supports(ArtType type) => true;
        public Task<ArtImage?> FetchAsync(string gameId, ArtType type, CancellationToken ct) => Task.FromResult(fetch());
    }

    [Fact]
    public async Task MissCache_RecordsNotFound_ButNotNetworkFailures()
    {
        using var tmp = new TempDir();
        var misses = new ArtMissCache(Path.Combine(tmp.Path, "misses.json"));
        var dl = new ArtDownloader(new ArtStore(Path.Combine(tmp.Path, "art")), misses);

        var notFound = new FakeSource(() => null);
        var offline = new FakeSource(() => throw new HttpRequestException("offline"));

        var r1 = await dl.DownloadManyAsync(["SLUS-20062"], [ArtType.Cover], [offline], overwrite: false);
        Assert.Equal(1, r1.Failed);
        Assert.False(misses.IsRecentMiss("SLUS-20062", ArtType.Cover));

        var r2 = await dl.DownloadManyAsync(["SLUS-20062"], [ArtType.Cover], [notFound], overwrite: false);
        Assert.Equal(1, r2.NotFound);
        Assert.True(new ArtMissCache(Path.Combine(tmp.Path, "misses.json")).IsRecentMiss("SLUS-20062", ArtType.Cover));  // persisted

        var r3 = await dl.DownloadManyAsync(["SLUS-20062"], [ArtType.Cover], [notFound], overwrite: false, skipRecentMisses: true);
        Assert.Equal(1, r3.Skipped);
    }
}

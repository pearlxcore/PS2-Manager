using System.IO.Compression;
using PS2Manager.Core.SaveStates;
using Xunit;

namespace PS2Manager.Tests;

public sealed class StateUpgraderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ps2m-sstates").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    /// <summary>A minimal .p2s: just the 36-byte version entry (format code in the high 16 bits).</summary>
    private string MakeState(string name, ushort format)
    {
        var path = Path.Combine(_dir, name);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        using var s = zip.CreateEntry("PCSX2 Savestate Version.id").Open();
        var data = new byte[36];
        BitConverter.GetBytes((uint)format << 16).CopyTo(data, 0);
        "v2.x"u8.ToArray().CopyTo(data, 4);
        s.Write(data);
        return path;
    }

    [Theory]
    [InlineData("2.8.2.0", "2.8.2")]
    [InlineData("2.8.0", "2.8.2")]
    [InlineData("v2.6.1", "2.6.3")]
    [InlineData("2.4", "2.4.0")]
    public void FindTarget_MapsInstalledVersionToRelease(string installed, string release) =>
        Assert.Equal(release, StateUpgrader.FindTarget(installed, Catalog.Load())?.Version);

    [Theory]
    [InlineData(null)]
    [InlineData("1.6.0")]
    [InlineData("2.9.100")]
    [InlineData("nightly")]
    public void FindTarget_UnknownVersions_ReturnNull(string? installed) =>
        Assert.Null(StateUpgrader.FindTarget(installed, Catalog.Load()));

    [Fact]
    public void ReadFormat_ReadsVersionEntry_AndRejectsJunk()
    {
        Assert.Equal((ushort)0x9A54, StateUpgrader.ReadFormat(MakeState("a.p2s", 0x9A54)));
        var junk = Path.Combine(_dir, "junk.p2s");
        File.WriteAllText(junk, "not a zip");
        Assert.Null(StateUpgrader.ReadFormat(junk));
    }

    [Fact]
    public void GameStates_MatchesSerialAndCrc()
    {
        MakeState("SLUS-20224 (20ED0A8A).01.p2s", 0x9A54);
        MakeState("SLUS-20224 (20ED0A8A).resume.p2s", 0x9A54);
        MakeState("SLUS-20224 (20ED0A8A).02.p2s.backup", 0x9A54);
        MakeState("SLUS-20224 (E7EA3288).01.p2s", 0x9A54);
        MakeState("SLUS-20013 (F77E2FB5).resume.p2s", 0x9A54);

        Assert.Equal(3, StateUpgrader.GameStates(_dir, "SLUS-20224", 0x20ED0A8A).Count);
        Assert.Equal(4, StateUpgrader.GameStates(_dir, "SLUS-20224", null).Count);
        Assert.Empty(StateUpgrader.GameStates(_dir, "SCUS-97177", null));
    }

    [Fact]
    public void Plan_IncludesOnlyOlderStatesWithARoute()
    {
        var old24 = MakeState("G (00000001).01.p2s", 0x9A54);    // 2.4.0
        var old20 = MakeState("G (00000001).02.p2s", 0x9A4F);    // 2.0.x
        var current = MakeState("G (00000001).03.p2s", 0x9A59);  // 2.8.x
        var unknown = MakeState("G (00000001).04.p2s", 0x1234);
        var junk = Path.Combine(_dir, "G (00000001).05.p2s");
        File.WriteAllText(junk, "x");

        var plan = StateUpgrader.Plan([old24, old20, current, unknown, junk], "2.8.2.0");

        Assert.NotNull(plan);
        Assert.Equal("2.8.2", plan.Target.Version);
        Assert.Equal([old24, old20], plan.States.Select(s => s.Path));
        Assert.Equal("2.4.0 and 2.0.0-2.0.3", plan.FromLabel);
    }

    [Fact]
    public void Plan_NothingToDo_ReturnsNull()
    {
        var current = MakeState("G (00000001).01.p2s", 0x9A59);
        Assert.Null(StateUpgrader.Plan([current], "2.8.2.0"));
        // Newer state than the installed PCSX2: no backward conversion.
        Assert.Null(StateUpgrader.Plan([current], "2.4.0"));
        // PCSX2 version the converter doesn't know.
        Assert.Null(StateUpgrader.Plan([MakeState("G (00000001).02.p2s", 0x9A54)], "1.6.0"));
    }
}

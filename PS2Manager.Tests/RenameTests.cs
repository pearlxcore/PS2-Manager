using PS2Manager.Core.Models;
using PS2Manager.Core.Rename;

namespace PS2Manager.Tests;

public class RenameTokenTests
{
    private static readonly GameEntry Game = TestDiscs.Entry(@"C:\games\gt4.iso");

    [Theory]
    [InlineData("{TITLE} [{GAME_ID}]", "Gran Turismo 4 [SLUS-20062]")]
    [InlineData("{GAME_ID_OPL}.{TITLE}", "SLUS_200.62.Gran Turismo 4")]
    [InlineData("{TITLE} ({REGION}) {DISC}", "Gran Turismo 4 (NTSC-U)")]
    [InlineData("{title} - {gameid}", "Gran Turismo 4 - SLUS-20062")]     // legacy lowercase tokens
    [InlineData("{TITLE} ({YEAR})", "Gran Turismo 4 (2005)")]
    [InlineData("{TITLE} [{CRC}]", "Gran Turismo 4")]                     // empty value -> brackets removed
    [InlineData("{TITLE} - {PUBLISHER} - {GAME_ID}", "Gran Turismo 4 - SLUS-20062")]
    [InlineData("{TITLE} {BOGUS}", "Gran Turismo 4")]
    public void Expand(string template, string expected) => Assert.Equal(expected, RenameTokens.Expand(template, Game));

    [Fact]
    public void Expand_SanitizesInvalidCharacters()
    {
        var g = TestDiscs.Entry(@"C:\x.iso", title: "Ratchet: Deadlocked / \"Gladiator\"?");
        Assert.Equal("Ratchet - Deadlocked - 'Gladiator'", RenameTokens.Expand("{TITLE}", g));
        var b = TestDiscs.Entry(@"C:\x.iso", title: "Burnout 3: Takedown");
        Assert.Equal("Burnout 3 - Takedown", RenameTokens.Expand("{TITLE}", b));
    }

    [Fact]
    public void Expand_MultiDisc()
    {
        var g = TestDiscs.Entry(@"C:\x.iso");
        g.DiscNumber = 2;
        Assert.Equal("Gran Turismo 4 Disc 2", RenameTokens.Expand("{TITLE} {DISC}", g));
    }

    [Fact]
    public void UnknownTokens_ReportsOnlyUnknown()
    {
        Assert.Equal(["{NOPE}"], RenameTokens.UnknownTokens("{TITLE} {NOPE} {gameid}"));
    }
}

public class FileOperationPlanTests
{
    [Fact]
    public async Task Rename_Apply_Undo_RoundTrip()
    {
        using var tmp = new TempDir();
        var a = TestDiscs.Entry(tmp.File("a.iso"));
        var b = TestDiscs.Entry(tmp.File("sub/b.bin"), id: "SCES-50000", title: "Other Game", region: "PAL");

        var plan = FileOperationPlan.Rename([a, b], "{TITLE} [{GAME_ID}]", ConflictMode.Skip);
        Assert.All(plan, p => Assert.Equal(PlanStatus.Ready, p.Status));
        Assert.Equal("Gran Turismo 4 [SLUS-20062].iso", plan[0].TargetName);
        Assert.Equal("Other Game [SCES-50000].bin", plan[1].TargetName);   // extension preserved

        var history = new RenameHistory(Path.Combine(tmp.Path, "h.json"));
        var result = await FileOperationPlan.ApplyAsync(plan, "test");
        history.Push(result.Batch!);

        Assert.Equal(2, result.Succeeded);
        Assert.True(File.Exists(Path.Combine(tmp.Path, "Gran Turismo 4 [SLUS-20062].iso")));
        Assert.EndsWith("Gran Turismo 4 [SLUS-20062].iso", a.IsoPath);

        // History survives reload
        var reloaded = new RenameHistory(Path.Combine(tmp.Path, "h.json"));
        var undone = reloaded.UndoLast(out var errors);
        Assert.Empty(errors);
        Assert.Equal(2, undone.Count);
        Assert.True(File.Exists(Path.Combine(tmp.Path, "a.iso")));
        Assert.True(File.Exists(Path.Combine(tmp.Path, "sub", "b.bin")));
    }

    [Fact]
    public void Rename_DetectsConflicts_WithinBatchAndOnDisk()
    {
        using var tmp = new TempDir();
        tmp.File("Gran Turismo 4.iso");                                  // already on disk, not in batch
        var a = TestDiscs.Entry(tmp.File("a.iso"));
        var b = TestDiscs.Entry(tmp.File("b.iso"), title: "Same");
        var c = TestDiscs.Entry(tmp.File("c.iso"), title: "Same");

        var plan = FileOperationPlan.Rename([a, b, c], "{TITLE}", ConflictMode.Skip);
        Assert.Equal(PlanStatus.Conflict, plan[0].Status);
        Assert.Equal(PlanStatus.Ready, plan[1].Status);
        Assert.Equal(PlanStatus.Conflict, plan[2].Status);
        Assert.False(plan[2].Include);
    }

    [Fact]
    public void Rename_AppendNumber_ResolvesConflicts()
    {
        using var tmp = new TempDir();
        var b = TestDiscs.Entry(tmp.File("b.iso"), title: "Same");
        var c = TestDiscs.Entry(tmp.File("c.iso"), title: "Same");

        var plan = FileOperationPlan.Rename([b, c], "{TITLE}", ConflictMode.AppendNumber);
        Assert.Equal("Same.iso", plan[0].TargetName);
        Assert.Equal("Same (2).iso", plan[1].TargetName);
    }

    [Fact]
    public void Rename_UnchangedAndMissing()
    {
        using var tmp = new TempDir();
        var same = TestDiscs.Entry(tmp.File("Gran Turismo 4.iso"));
        var missing = TestDiscs.Entry(Path.Combine(tmp.Path, "gone.iso"));
        var plan = FileOperationPlan.Rename([same, missing], "{TITLE}", ConflictMode.Skip);
        Assert.Equal(PlanStatus.Unchanged, plan[0].Status);
        Assert.Equal(PlanStatus.SourceMissing, plan[1].Status);
    }

    [Fact]
    public async Task Rename_CaseOnlyChange_Works()
    {
        using var tmp = new TempDir();
        var g = TestDiscs.Entry(tmp.File("gran turismo 4.iso"));
        var plan = FileOperationPlan.Rename([g], "{TITLE}", ConflictMode.Skip);
        Assert.Equal(PlanStatus.Ready, plan[0].Status);
        await FileOperationPlan.ApplyAsync(plan, "case");
        Assert.Contains("Gran Turismo 4.iso", Directory.GetFiles(tmp.Path).Select(Path.GetFileName));
    }

    [Fact]
    public void MoveToFolders_DoesNotNestTwice()
    {
        using var tmp = new TempDir();
        var inFolder = TestDiscs.Entry(tmp.File("Gran Turismo 4/gt4.iso"));
        var loose = TestDiscs.Entry(tmp.File("other.iso"), title: "Other");
        var plan = FileOperationPlan.MoveToFolders([inFolder, loose], "{TITLE}", null, ConflictMode.Skip);
        Assert.Equal(PlanStatus.Unchanged, plan[0].Status);
        Assert.Equal(Path.Combine(tmp.Path, "Other", "other.iso"), plan[1].TargetPath);
    }

    [Fact]
    public void FixExtensions_UsesRealLayout()
    {
        using var tmp = new TempDir();
        var rawAsIso = TestDiscs.Entry(tmp.File("raw.iso"));
        rawAsIso.IsRawCdImage = true;
        var isoAsBin = TestDiscs.Entry(tmp.File("dvd.bin"));
        isoAsBin.SizeBytes = 2048 * 1000;
        var plan = FileOperationPlan.FixExtensions([rawAsIso, isoAsBin], ConflictMode.Skip);
        Assert.Equal("raw.bin", plan[0].TargetName);
        Assert.Equal("dvd.iso", plan[1].TargetName);
    }
}

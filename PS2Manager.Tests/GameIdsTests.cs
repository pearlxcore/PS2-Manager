using PS2Manager.Core.Models;

namespace PS2Manager.Tests;

public class GameIdsTests
{
    [Theory]
    [InlineData("SLUS_200.62", "SLUS-20062")]
    [InlineData("slus-20062", "SLUS-20062")]
    [InlineData("SLUS20062", "SLUS-20062")]
    [InlineData("SCES_500.00", "SCES-50000")]
    [InlineData("", "Unknown")]
    [InlineData(null, "Unknown")]
    [InlineData("unknown", "Unknown")]
    public void Normalize(string? input, string expected) => Assert.Equal(expected, GameIds.Normalize(input));

    [Theory]
    [InlineData("SLUS-20062", "SLUS_200.62")]
    [InlineData("SLUS_200.62", "SLUS_200.62")]
    [InlineData("SCES-50000", "SCES_500.00")]
    public void ToOplId(string input, string expected) => Assert.Equal(expected, GameIds.ToOplId(input));

    [Theory]
    [InlineData("BOOT2 = cdrom0:\\SLUS_200.62;1", "SLUS-20062")]
    [InlineData("Gran Turismo 4 [SCES-51719].iso", "SCES-51719")]
    [InlineData("no id here", "Unknown")]
    public void Extract(string text, string expected) => Assert.Equal(expected, GameIds.Extract(text));

    [Theory]
    [InlineData("SLUS-20062", "NTSC-U")]
    [InlineData("SCES-50000", "PAL")]
    [InlineData("SLPM-65000", "NTSC-J")]
    [InlineData("XXXX-00000", "")]
    public void DetectRegion(string id, string expected) => Assert.Equal(expected, GameIds.DetectRegion(id));
}

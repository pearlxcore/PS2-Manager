using PS2Manager.Core.Updates;
using Xunit;

namespace PS2Manager.Tests;

public class UpdateCheckerTests
{
    [Theory]
    [InlineData("v2.1.0", "2.1.0")]
    [InlineData("2.2", "2.2.0")]
    [InlineData("V3.0.1-beta", "3.0.1")]
    [InlineData(" v10.4.2 ", "10.4.2")]
    public void ParseTag_HandlesCommonFormats(string tag, string expected) =>
        Assert.Equal(Version.Parse(expected), UpdateChecker.ParseTag(tag));

    [Theory]
    [InlineData("0.1")]
    [InlineData("release")]
    [InlineData("")]
    public void ParseTag_RejectsOrKeepsOldFormats(string tag)
    {
        var v = UpdateChecker.ParseTag(tag);
        Assert.True(v is null || v < new Version(2, 0));
    }

    [Theory]
    [InlineData("2.1.1", "2.1.0", true)]
    [InlineData("2.2.0", "2.1.9", true)]
    [InlineData("3.0", "2.9.9", true)]
    [InlineData("2.1.0", "2.1.0", false)]
    [InlineData("2.1.0", "2.1.0.0", false)]
    [InlineData("2.0.0", "2.1.0", false)]
    public void IsNewer_ComparesMajorMinorPatch(string latest, string current, bool newer) =>
        Assert.Equal(newer, UpdateChecker.IsNewer(Version.Parse(latest), Version.Parse(current)));
}

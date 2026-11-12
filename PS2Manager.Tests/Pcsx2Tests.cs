using PS2Manager.Core.Pcsx2;
using Xunit;

namespace PS2Manager.Tests;

public class Pcsx2Tests
{
    [Fact]
    public void LaunchArguments_PutFileAfterDoubleDash()
    {
        var args = new Pcsx2LaunchOptions(Pcsx2Window.Fullscreen, Pcsx2Boot.Fast, NoGui: true).BuildArguments(@"D:\PS2\DVD\-weird.iso");
        Assert.Equal(["-fullscreen", "-fastboot", "-nogui", "--", @"D:\PS2\DVD\-weird.iso"], args);
    }

    [Fact]
    public void LaunchArguments_DefaultsPassNoOverrides()
    {
        var args = new Pcsx2LaunchOptions().BuildArguments("game.iso");
        Assert.Equal(["--", "game.iso"], args);
    }

    [Fact]
    public void GameIndex_ReadsTopLevelCompatOnly()
    {
        string path = Path.GetTempFileName();
        File.WriteAllText(path, """
            SLUS-20062:
              name: "Grand Theft Auto III"
              region: "NTSC-U"
              compat: 5
            SLUS-20064:
              name: "Oni"
              patches:
                FD9CD8FC:
                  content: |-
                    comment=compat: 1 inside a patch must not count
            SCES-50000:
              name: "Something"
              compat: 6
            """);
        try
        {
            var map = Pcsx2GameIndex.Load(path);
            Assert.Equal(Pcsx2Compat.Playable, map["SLUS-20062"]);
            Assert.Equal(Pcsx2Compat.Perfect, map["sces-50000"]);
            Assert.False(map.ContainsKey("SLUS-20064"));
        }
        finally { File.Delete(path); }
    }

    private const string Ini = "[UI]\r\nTheme = dark\r\n\r\n[GameList]\r\nRecursivePaths = \\\\nas\\PS2\r\nRecursivePaths = D:\\PS2\\DVD\r\n\r\n\r\n[GameListTableView]\r\nHeaderState = AAAA\r\n";

    [Fact]
    public void Ini_ReadsGameListPaths()
    {
        var paths = Pcsx2Ini.ReadGameListPaths(Ini);
        Assert.Equal(2, paths.Count);
        Assert.Contains((@"D:\PS2\DVD", true), paths);
    }

    [Fact]
    public void Ini_AddsOnlyUncoveredFolders_InsideSection_PreservingRest()
    {
        var (text, added) = Pcsx2Ini.AddRecursivePaths(Ini, [@"D:\PS2\DVD", @"D:\PS2\DVD\Sub", @"D:\PS2\CD", @"d:\ps2\cd\"]);
        Assert.Equal([@"D:\PS2\CD"], added);
        Assert.Contains("RecursivePaths = D:\\PS2\\DVD\r\nRecursivePaths = D:\\PS2\\CD\r\n", text);
        Assert.Contains("[GameListTableView]\r\nHeaderState = AAAA", text);
        Assert.StartsWith("[UI]\r\nTheme = dark", text);
        Assert.Equal(Ini.Length + "RecursivePaths = D:\\PS2\\CD\r\n".Length, text.Length);   // nothing else changed
    }

    [Fact]
    public void Ini_CreatesSectionWhenMissing()
    {
        var (text, added) = Pcsx2Ini.AddRecursivePaths("[UI]\nTheme = dark\n", [@"E:\Games"]);
        Assert.Single(added);
        Assert.EndsWith("[GameList]\nRecursivePaths = E:\\Games\n", text);
    }

    [Fact]
    public void Install_DetectsPortableData()
    {
        string dir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            string exe = Path.Combine(dir, Pcsx2Install.ExeName);
            File.WriteAllText(exe, "");
            Assert.Equal(Path.Combine(@"C:\Docs", "PCSX2"), Pcsx2Install.FromExe(exe, @"C:\Docs").DataDir);
            File.WriteAllText(Path.Combine(dir, "portable.ini"), "");
            Assert.True(Pcsx2Install.FromExe(exe, @"C:\Docs").IsPortable);
        }
        finally { Directory.Delete(dir, true); }
    }
}

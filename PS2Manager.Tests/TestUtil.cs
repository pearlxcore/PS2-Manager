using System.Text;
using DiscUtils.Iso9660;
using PS2Manager.Core;
using PS2Manager.Core.Models;

namespace PS2Manager.Tests;

/// <summary>Per-test temp directory, also used as the app data root.</summary>
public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ps2mgr-tests", Guid.NewGuid().ToString("N"));

    public TempDir()
    {
        Directory.CreateDirectory(Path);
        AppPaths.SetRoot(System.IO.Path.Combine(Path, "_appdata"));
    }

    public string File(string relative, string content = "x")
    {
        string p = System.IO.Path.Combine(Path, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(p)!);
        System.IO.File.WriteAllText(p, content);
        return p;
    }

    public void Dispose()
    {
        try { Directory.Delete(Path, true); } catch { }
    }
}

public static class TestDiscs
{
    /// <summary>Builds a minimal PS2-like ISO (2048-byte sectors) with SYSTEM.CNF and a boot ELF.</summary>
    public static byte[] BuildIso(string elfName = "SLUS_200.62", string version = "1.01", string label = "GT_TEST", byte[]? elf = null)
    {
        var builder = new CDBuilder { UseJoliet = false, VolumeIdentifier = label };
        builder.AddFile("SYSTEM.CNF", Encoding.ASCII.GetBytes($"BOOT2 = cdrom0:\\{elfName};1\r\nVER = {version}\r\nVMODE = NTSC\r\n"));
        builder.AddFile(elfName, elf ?? Enumerable.Range(0, 4096).Select(i => (byte)i).ToArray());
        using var ms = new MemoryStream();
        builder.Build(ms);
        return ms.ToArray();
    }

    /// <summary>Wraps 2048-byte sectors into raw 2352-byte Mode 2 sectors (24-byte header, 280-byte EDC/ECC tail).</summary>
    public static byte[] ToRawCd(byte[] iso)
    {
        int sectors = iso.Length / 2048;
        var raw = new byte[sectors * 2352];
        for (int s = 0; s < sectors; s++)
            Buffer.BlockCopy(iso, s * 2048, raw, s * 2352 + 24, 2048);
        // Make sure the size is not also a multiple of 2048 so auto-detection kicks in.
        if (raw.Length % 2048 == 0) Array.Resize(ref raw, raw.Length + 2352);
        return raw;
    }

    public static GameEntry Entry(string path, string id = "SLUS-20062", string title = "Gran Turismo 4", string region = "NTSC-U") =>
        new()
        {
            IsoPath = path,
            GameId = id,
            Version = "1.01",
            Info = new GameInfo { Title = title, Region = region, Release = "2005-02-22", Developer = "Polyphony Digital" },
        };
}

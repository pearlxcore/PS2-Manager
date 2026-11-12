using System.Buffers;
using System.IO.Hashing;
using DiscUtils.Iso9660;

namespace PS2Manager.Core.Iso;

public static class Checksums
{
    /// <summary>PCSX2-style game CRC: XOR of every little-endian 32-bit word of the boot ELF.</summary>
    public static uint Pcsx2ElfCrc(Stream elf)
    {
        uint crc = 0;
        var buffer = new byte[64 * 1024];
        int carry = 0;
        int read;
        while ((read = elf.Read(buffer, carry, buffer.Length - carry)) > 0)
        {
            int total = carry + read;
            int words = total / 4;
            for (int i = 0; i < words; i++)
                crc ^= BitConverter.ToUInt32(buffer, i * 4);
            carry = total % 4;
            if (carry > 0) Buffer.BlockCopy(buffer, words * 4, buffer, 0, carry);
        }
        return crc;
    }

    public static uint Pcsx2ElfCrc(CDReader cd, string path)
    {
        using var s = cd.OpenFile(path, FileMode.Open);
        return Pcsx2ElfCrc(s);
    }

    /// <summary>Standard CRC32 of the whole image (what Redump lists).</summary>
    public static async Task<uint> FileCrc32Async(string path, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var crc = new Crc32();
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(4 << 20);
        try
        {
            long done = 0, total = fs.Length;
            int read;
            while ((read = await fs.ReadAsync(buffer, ct)) > 0)
            {
                crc.Append(buffer.AsSpan(0, read));
                done += read;
                progress?.Report(total == 0 ? 1 : (double)done / total);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
        return crc.GetCurrentHashAsUInt32();
    }
}

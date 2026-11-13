using System.Buffers.Binary;
using System.Security.Cryptography;
using SharpCompress.Archives;
using SharpCompress.Readers;

namespace PS2Manager.Core.SaveStates;

public sealed record Inspection(string Path, ushort? Format, string? BuildTag, string? KnownRelease, string? Sha256,
    string[] Entries, string[] MissingRequired, string[] MissingOptional, string? Error)
{
    public bool IsValid => Error is null && MissingRequired.Length == 0;
    public string FormatText => Format is null ? "invalid" : $"0x{Format:X4}";
}

/// <summary>
/// Read-only structural inspection of a .p2s archive: streams and CRC-checks every entry,
/// reads the version indicator, and checks the component set. Never modifies the file.
/// </summary>
public static class SaveStateInspector
{
    private const int MaxArchiveEntries = 4096;
    private const long MaxEntryBytes = 1024L * 1024 * 1024;
    private const long MaxTotalBytes = 2L * 1024 * 1024 * 1024;

    public static readonly string[] RequiredEntries =
    [
        "PCSX2 Internal Structures.dat", "eeMemory.bin", "iopMemory.bin", "eeHwRegs.bin",
        "iopHwRegs.bin", "Scratchpad.bin", "vu0Memory.bin", "vu1Memory.bin",
        "vu0MicroMem.bin", "vu1MicroMem.bin", "SPU2.bin", "PAD.bin", "GS.bin"
    ];

    public static readonly string[] OptionalEntries = ["USB.bin", "Achievements.bin"];

    public static Inspection Inspect(string path, Catalog? catalog = null)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) throw new FileNotFoundException("File not found", path);
            if (info.Length > MaxTotalBytes) throw new InvalidDataException("Archive exceeds the 2 GiB safety limit.");
            using var sha = SHA256.Create();
            using var file = File.OpenRead(path);
            var hash = Convert.ToHexString(sha.ComputeHash(file));
            file.Position = 0;
            using var archive = ArchiveFactory.OpenArchive(file, new ReaderOptions { LeaveStreamOpen = true });
            var archiveEntries = archive.Entries.ToArray();
            if (archiveEntries.Length > MaxArchiveEntries) throw new InvalidDataException("Archive contains too many entries.");
            long total = 0;
            var names = new HashSet<string>(StringComparer.Ordinal);
            var descriptions = new List<string>();
            foreach (var entry in archiveEntries)
            {
                var entryName = entry.Key ?? throw new InvalidDataException("Archive contains an entry with no name.");
                if (!names.Add(entryName)) throw new InvalidDataException($"Duplicate archive entry: {entryName}");
                if (entry.Size < 0 || entry.Size > MaxEntryBytes) throw new InvalidDataException($"Entry exceeds the 1 GiB safety limit: {entryName}");
                total = checked(total + entry.Size);
                if (total > MaxTotalBytes) throw new InvalidDataException("Expanded archive exceeds the 2 GiB safety limit.");
                if (entry.Size > 0 && (entry.CompressedSize == 0 || entry.Size / Math.Max(1, entry.CompressedSize) > 1000))
                    throw new InvalidDataException($"Suspicious compression ratio: {entryName}");
                // Drain each stream: this verifies decompression and CRC without writing data to disk.
                using var input = entry.OpenEntryStream();
                var buffer = new byte[64 * 1024];
                long readTotal = 0;
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    readTotal += read;
                    if (readTotal > entry.Size || readTotal > MaxEntryBytes) throw new InvalidDataException($"Entry expanded beyond its declared length: {entryName}");
                }
                if (readTotal != entry.Size) throw new InvalidDataException($"Truncated archive entry: {entryName}");
                descriptions.Add($"{entryName} ({entry.Size} bytes; {entry.CompressedSize} compressed bytes; {entry.CompressionType})");
            }

            var versionEntry = archiveEntries.FirstOrDefault(x => x.Key == "PCSX2 Savestate Version.id") ?? throw new InvalidDataException("Missing version indicator entry.");
            if (versionEntry.Size != 36) throw new InvalidDataException($"Version indicator has unexpected size {versionEntry.Size}; expected 36 bytes.");
            byte[] versionData;
            using (var stream = versionEntry.OpenEntryStream())
            {
                versionData = new byte[36];
                ReadExactly(stream, versionData);
            }
            var raw = BinaryPrimitives.ReadUInt32LittleEndian(versionData);
            var format = (ushort)(raw >> 16);
            var buildBytes = versionData.AsSpan(4, 32);
            var terminator = buildBytes.IndexOf((byte)0);
            if (terminator >= 0) buildBytes = buildBytes[..terminator];
            var build = System.Text.Encoding.UTF8.GetString(buildBytes);
            var release = (catalog ?? Catalog.Load()).FindFormat(format);
            var missing = RequiredEntries.Where(x => !names.Contains(x)).ToArray();
            var optionalMissing = OptionalEntries.Where(x => !names.Contains(x)).ToArray();
            return new(path, format, build, release?.Label, hash, descriptions.ToArray(), missing, optionalMissing, null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new(path, null, null, null, null, [], [], [], ex.Message);
        }
    }

    public static bool IsStructurallyValidState(string path, out string? error)
    {
        var result = Inspect(path);
        error = result.Error ?? (result.MissingRequired.Length > 0
            ? $"missing required components: {string.Join(", ", result.MissingRequired)}"
            : null);
        return error is null;
    }

    private static void ReadExactly(Stream stream, byte[] buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var count = stream.Read(buffer, offset, buffer.Length - offset);
            if (count == 0) throw new InvalidDataException("Truncated version indicator.");
            offset += count;
        }
    }
}

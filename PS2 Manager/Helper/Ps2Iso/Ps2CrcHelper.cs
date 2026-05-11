using System;
using System.IO;
using DiscUtils.Iso9660;

public static class Ps2CrcHelper
{
    // ============================
    // FAST PCSX2-STYLE ELF CRC
    // ============================

    public static uint ComputePcsx2Crc(Stream elfStream)
    {
        uint crc = 0;
        byte[] buffer = new byte[4];

        while (elfStream.Read(buffer, 0, 4) == 4)
        {
            uint word =
                (uint)(buffer[0] |
                      (buffer[1] << 8) |
                      (buffer[2] << 16) |
                      (buffer[3] << 24));

            crc ^= word;
        }

        return crc;
    }

    public static uint ComputeElfCrcPCSX2(CDReader cd, string elfPath)
    {
        using var stream = cd.OpenFile(elfPath, FileMode.Open);
        return ComputePcsx2Crc(stream);
    }

    // ============================
    // Full ISO CRC32 (optional slow)
    // ============================
    public static uint ComputeIsoCrc(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        uint crc = 0xFFFFFFFF;
        byte[] buffer = new byte[1024 * 1024]; // 1MB chunks

        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                byte index = (byte)((crc ^ buffer[i]) & 0xFF);
                crc = (crc >> 8) ^ Crc32Table[index];
            }
        }

        return crc ^ 0xFFFFFFFF;
    }

    // ============================
    // CRC TABLE (standard poly 0xEDB88320)
    // ============================
    private static readonly uint[] Crc32Table = CreateTable();

    private static uint[] CreateTable()
    {
        uint[] table = new uint[256];

        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int j = 0; j < 8; j++)
                crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;

            table[i] = crc;
        }

        return table;
    }
}

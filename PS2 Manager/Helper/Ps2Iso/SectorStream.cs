namespace PS2_ISO_Manager.Helper.Ps2Iso;

/// <summary>
/// Wraps a raw CD stream (2352 bytes/sector) and exposes it as
/// DVD-style (2048 bytes/sector) by stripping the 24-byte sync/header.
/// Pass-through for already-DVD streams (no conversion).
/// </summary>
public sealed class SectorStream : Stream
{
    private readonly Stream _inner;
    private readonly int _rawSectorSize;
    private readonly int _dataSectorSize;
    private readonly int _headerSize;
    private readonly long _dataLength;
    private long _position;

    public SectorStream(Stream inner, bool forceCd = false)
    {
        _inner = inner;
        long fileSize = inner.Length;

        // Detect: DVD = multiple of 2048, CD mode 2 = multiple of 2352
        // forceCd overrides detection for ambiguous sizes (divisible by both)
        bool isCd = forceCd || (fileSize % 2352 == 0 && fileSize % 2048 != 0);

        if (isCd)
        {
            _rawSectorSize = 2352;
            _dataSectorSize = 2048;
            _headerSize = 24;
        }
        else
        {
            // DVD or already-correct — pass through
            _rawSectorSize = 2048;
            _dataSectorSize = 2048;
            _headerSize = 0;
        }

        _dataLength = (fileSize / _rawSectorSize) * _dataSectorSize;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _dataLength;

    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_headerSize == 0)
        {
            _inner.Position = _position;
            int read = _inner.Read(buffer, offset, count);
            _position += read;
            return read;
        }

        int totalRead = 0;
        while (totalRead < count && _position < _dataLength)
        {
            long sector = _position / _dataSectorSize;
            long sectorOffset = _position % _dataSectorSize;
            long rawOffset = sector * _rawSectorSize + _headerSize + sectorOffset;

            _inner.Position = rawOffset;
            int toRead = (int)Math.Min(count - totalRead, _dataSectorSize - sectorOffset);
            int read = _inner.Read(buffer, offset + totalRead, toRead);
            if (read == 0) break;

            totalRead += read;
            _position += read;
        }
        return totalRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        _position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _dataLength + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };
        return _position;
    }

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

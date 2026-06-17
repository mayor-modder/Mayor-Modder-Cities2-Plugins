namespace SaveInvestigator;

public sealed class SaveGameDataCursor
{
    private const byte RawBufferFormat = 0;
    private const byte CompressedZstdBufferFormat = 2;

    private readonly byte[] _buffer;
    private int _position;

    public SaveGameDataCursor(byte[] buffer)
    {
        _buffer = buffer;
    }

    public static SaveGameDataCursor AdvanceToArchetypeBuffers(byte[] payload, byte bufferFormat)
    {
        var cursor = new SaveGameDataCursor(payload);
        cursor.ReadSizedBuffer();
        cursor.ReadNextOuterBuffer(bufferFormat);
        cursor.ReadNextOuterBuffer(bufferFormat);
        cursor.ReadNextOuterBuffer(bufferFormat);
        return cursor;
    }

    public byte ReadByte()
    {
        EnsureAvailable(sizeof(byte));
        return _buffer[_position++];
    }

    public ushort ReadUInt16()
    {
        EnsureAvailable(sizeof(ushort));
        var value = BitConverter.ToUInt16(_buffer, _position);
        _position += sizeof(ushort);
        return value;
    }

    public int ReadInt32()
    {
        EnsureAvailable(sizeof(int));
        var value = BitConverter.ToInt32(_buffer, _position);
        _position += sizeof(int);
        return value;
    }

    public float ReadSingle()
    {
        EnsureAvailable(sizeof(float));
        var value = BitConverter.ToSingle(_buffer, _position);
        _position += sizeof(float);
        return value;
    }

    public byte[] ReadBlock()
    {
        return ReadSizedBuffer();
    }

    public byte[] ReadNextOuterBuffer(byte bufferFormat)
    {
        return ReadNextOuterBufferWithMetadata(bufferFormat).Data;
    }

    public OuterBufferBlock ReadNextOuterBufferWithMetadata(byte bufferFormat)
    {
        return bufferFormat switch
        {
            RawBufferFormat => ReadRawOuterBuffer(),
            CompressedZstdBufferFormat => ReadCompressedZstdOuterBuffer(),
            _ => throw new NotSupportedException($"Unsupported save buffer format: {bufferFormat}")
        };
    }

    public byte[] ReadSizedBuffer()
    {
        var length = ReadInt32();
        EnsureAvailable(length);
        var buffer = new byte[length];
        Buffer.BlockCopy(_buffer, _position, buffer, 0, length);
        _position += length;
        return buffer;
    }

    public int RemainingByteCount => _buffer.Length - _position;

    public byte[] ReadRemainingBytes()
    {
        var remaining = new byte[RemainingByteCount];
        Buffer.BlockCopy(_buffer, _position, remaining, 0, remaining.Length);
        _position = _buffer.Length;
        return remaining;
    }

    private OuterBufferBlock ReadRawOuterBuffer()
    {
        var buffer = ReadSizedBuffer();
        return new OuterBufferBlock(buffer.Length, buffer.Length, buffer);
    }

    private OuterBufferBlock ReadCompressedZstdOuterBuffer()
    {
        var uncompressedSize = ReadInt32();
        var compressedSize = ReadInt32();
        EnsureAvailable(compressedSize);

        var compressed = new byte[compressedSize];
        Buffer.BlockCopy(_buffer, _position, compressed, 0, compressedSize);
        _position += compressedSize;

        var decompressed = new ZstdSharp.Decompressor().Unwrap(compressed).ToArray();
        if (decompressed.Length != uncompressedSize)
        {
            throw new InvalidOperationException("Compressed save buffer decompressed to an unexpected size.");
        }

        return new OuterBufferBlock(uncompressedSize, compressedSize, decompressed);
    }

    private void EnsureAvailable(int length)
    {
        if (_position + length > _buffer.Length)
        {
            throw new InvalidOperationException("Unexpected end of save data payload.");
        }
    }
}

public sealed record OuterBufferBlock(
    int UncompressedSize,
    int CompressedSize,
    byte[] Data);

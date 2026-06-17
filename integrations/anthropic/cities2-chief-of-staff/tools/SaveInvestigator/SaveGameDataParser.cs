using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class SaveGameDataParser
{
    public static SavePreludeSummary ParsePrelude(byte[] payload)
    {
        var outer = new SaveGameDataCursor(payload);

        var metadataBuffer = outer.ReadSizedBuffer();
        var metadataCursor = new ByteCursor(metadataBuffer);
        var version = ReadVersion(metadataCursor);
        var bufferFormat = metadataCursor.ReadByte();
        var formatTags = ReadStrings(metadataCursor, metadataCursor.ReadUInt16());

        var componentTypes = ParseComponentTypes(outer.ReadNextOuterBuffer(bufferFormat));
        var systemTypes = ParseSystemTypes(outer.ReadNextOuterBuffer(bufferFormat));
        var archetypes = ParseArchetypes(outer.ReadNextOuterBuffer(bufferFormat));

        return new SavePreludeSummary(
            version,
            bufferFormat,
            new ReadOnlyCollection<string>(formatTags),
            new ReadOnlyCollection<ComponentTypeSummary>(componentTypes),
            new ReadOnlyCollection<SystemTypeSummary>(systemTypes),
            new ReadOnlyCollection<ArchetypeSummary>(archetypes));
    }

    private static SerializedVersion ReadVersion(ByteCursor cursor)
    {
        var versionVersion = cursor.ReadByte();
        if (versionVersion == 0)
        {
            cursor.Skip(3);
            return new SerializedVersion(versionVersion, 0, 0);
        }

        return new SerializedVersion(versionVersion, cursor.ReadInt64(), cursor.ReadInt32());
    }

    private static List<string> ReadStrings(ByteCursor cursor, int count)
    {
        var results = new List<string>(count);
        for (var index = 0; index < count; index += 1)
        {
            results.Add(cursor.ReadString());
        }

        return results;
    }

    private static List<ComponentTypeSummary> ParseComponentTypes(byte[] buffer)
    {
        var cursor = new ByteCursor(buffer);
        var count = cursor.ReadInt32();
        var results = new List<ComponentTypeSummary>(count);

        for (var index = 0; index < count; index += 1)
        {
            var block = cursor.ReadSizedBuffer();
            var blockCursor = new ByteCursor(block);
            var serializerType = blockCursor.ReadByte();
            var typeName = blockCursor.ReadString();
            results.Add(new ComponentTypeSummary(index, serializerType, typeName));
        }

        return results;
    }

    private static List<SystemTypeSummary> ParseSystemTypes(byte[] buffer)
    {
        var cursor = new ByteCursor(buffer);
        var count = cursor.ReadInt32();
        var results = new List<SystemTypeSummary>(count);

        for (var index = 0; index < count; index += 1)
        {
            var block = cursor.ReadSizedBuffer();
            var blockCursor = new ByteCursor(block);
            results.Add(new SystemTypeSummary(index, blockCursor.ReadString()));
        }

        return results;
    }

    private static List<ArchetypeSummary> ParseArchetypes(byte[] buffer)
    {
        var cursor = new ByteCursor(buffer);
        var count = cursor.ReadInt32();
        var results = new List<ArchetypeSummary>(count);

        for (var index = 0; index < count; index += 1)
        {
            var block = cursor.ReadSizedBuffer();
            var blockCursor = new ByteCursor(block);
            var entityCount = blockCursor.ReadInt32();
            var componentCount = blockCursor.ReadInt32();
            var componentIndexes = new List<int>(componentCount);

            for (var componentIndex = 0; componentIndex < componentCount; componentIndex += 1)
            {
                componentIndexes.Add(blockCursor.ReadInt32());
            }

            results.Add(new ArchetypeSummary(
                index,
                entityCount,
                new ReadOnlyCollection<int>(componentIndexes)));
        }

        return results;
    }

    private sealed class ByteCursor
    {
        private readonly byte[] _buffer;
        private int _position;

        public ByteCursor(byte[] buffer)
        {
            _buffer = buffer;
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

        public long ReadInt64()
        {
            EnsureAvailable(sizeof(long));
            var value = BitConverter.ToInt64(_buffer, _position);
            _position += sizeof(long);
            return value;
        }

        public string ReadString()
        {
            var length = ReadInt32();
            var characters = new char[length];

            for (var index = 0; index < length; index += 1)
            {
                characters[index] = ReadUtf8Char();
            }

            return new string(characters);
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

        public void Skip(int byteCount)
        {
            EnsureAvailable(byteCount);
            _position += byteCount;
        }

        private char ReadUtf8Char()
        {
            var first = ReadByte();
            if ((first & 0x80) == 0)
            {
                return (char)first;
            }

            if ((first & 0xE0) == 0xC0)
            {
                var second = ReadByte();
                var codePoint = ((first & 0x1F) << 6) | (second & 0x3F);
                return (char)codePoint;
            }

            if ((first & 0xF0) == 0xE0)
            {
                var second = ReadByte();
                var third = ReadByte();
                var codePoint = ((first & 0x0F) << 12) |
                                ((second & 0x3F) << 6) |
                                (third & 0x3F);
                return (char)codePoint;
            }

            throw new InvalidOperationException("Unsupported UTF-8 character width in save payload.");
        }

        private void EnsureAvailable(int length)
        {
            if (_position + length > _buffer.Length)
            {
                throw new InvalidOperationException("Unexpected end of save data payload.");
            }
        }
    }
}

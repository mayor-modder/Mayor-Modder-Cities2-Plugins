using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class GenericComponentDecoder
{
    public static bool TryDecodeInt32Lane(byte[] block, int entityCount, IList<int> values)
    {
        var bytesPerEntity = ArchetypeBufferWalker.TryGetBytesPerEntity(block.Length, entityCount);
        if (entityCount <= 0 || values.Count != entityCount || bytesPerEntity != sizeof(int))
        {
            return false;
        }

        for (var entityOrdinal = 0; entityOrdinal < entityCount; entityOrdinal += 1)
        {
            values[entityOrdinal] = BitConverter.ToInt32(block, entityOrdinal * bytesPerEntity.Value);
        }

        return true;
    }

    public static bool TryDecodeSingleLane(byte[] block, int entityCount, IList<float> values)
    {
        var bytesPerEntity = ArchetypeBufferWalker.TryGetBytesPerEntity(block.Length, entityCount);
        if (entityCount <= 0 || values.Count != entityCount || bytesPerEntity != sizeof(float))
        {
            return false;
        }

        for (var entityOrdinal = 0; entityOrdinal < entityCount; entityOrdinal += 1)
        {
            values[entityOrdinal] = BitConverter.ToSingle(block, entityOrdinal * bytesPerEntity.Value);
        }

        return true;
    }

    public static bool TryDecodeLeadingInt32Lane(byte[] block, int entityCount, IList<int> values)
    {
        var bytesPerEntity = ArchetypeBufferWalker.TryGetBytesPerEntity(block.Length, entityCount);
        if (entityCount <= 0 ||
            values.Count != entityCount ||
            !bytesPerEntity.HasValue ||
            bytesPerEntity.Value < sizeof(int))
        {
            return false;
        }

        for (var entityOrdinal = 0; entityOrdinal < entityCount; entityOrdinal += 1)
        {
            values[entityOrdinal] = BitConverter.ToInt32(block, entityOrdinal * bytesPerEntity.Value);
        }

        return true;
    }

    public static ReadOnlyCollection<PrimitiveComponentSampleFact> DecodeSamples(
        byte[] block,
        int entityCount,
        int entityIndexBase,
        int archetypeIndex,
        int sampleLimit)
    {
        var bytesPerEntity = ArchetypeBufferWalker.TryGetBytesPerEntity(block.Length, entityCount);
        if (entityCount <= 0 || !bytesPerEntity.HasValue || bytesPerEntity.Value <= 0 || sampleLimit <= 0)
        {
            return new ReadOnlyCollection<PrimitiveComponentSampleFact>([]);
        }

        var sampleCount = Math.Min(entityCount, sampleLimit);
        var samples = new List<PrimitiveComponentSampleFact>(sampleCount);
        for (var entityOrdinal = 0; entityOrdinal < sampleCount; entityOrdinal += 1)
        {
            var offset = entityOrdinal * bytesPerEntity.Value;
            var rawBytes = new byte[bytesPerEntity.Value];
            Buffer.BlockCopy(block, offset, rawBytes, 0, bytesPerEntity.Value);
            samples.Add(
                new PrimitiveComponentSampleFact(
                    entityIndexBase + entityOrdinal,
                    archetypeIndex,
                    entityOrdinal,
                    string.Join(" ", rawBytes.Select(value => value.ToString("X2"))),
                    bytesPerEntity.Value >= sizeof(int) ? BitConverter.ToInt32(block, offset) : null));
        }

        return new ReadOnlyCollection<PrimitiveComponentSampleFact>(samples);
    }

    public static bool TryDecodeCountPrefixedBuffer(
        byte[] block,
        int entityCount,
        int entityIndexBase,
        int archetypeIndex,
        int entrySize,
        Func<byte[], ReadOnlyCollection<DynamicBufferStructuredValueFact>> structuredDecoder,
        out ReadOnlyCollection<DynamicBufferEntityFact> entities)
    {
        if (entityCount <= 0 || entrySize <= 0)
        {
            entities = new ReadOnlyCollection<DynamicBufferEntityFact>([]);
            return false;
        }

        var results = new List<DynamicBufferEntityFact>(entityCount);
        var offset = 0;

        for (var entityOrdinal = 0; entityOrdinal < entityCount; entityOrdinal += 1)
        {
            if (offset > block.Length - sizeof(int))
            {
                entities = new ReadOnlyCollection<DynamicBufferEntityFact>(results);
                return false;
            }

            var headerValue = BitConverter.ToInt32(block, offset);
            var elementCount = headerValue;
            offset += sizeof(int);

            if (elementCount < 0)
            {
                entities = new ReadOnlyCollection<DynamicBufferEntityFact>(results);
                return false;
            }

            var bytesRemaining = block.Length - offset;
            var bytesNeededForEntries = checked(elementCount * entrySize);
            var minimumBytesForRemainingHeaders = checked((entityCount - entityOrdinal - 1) * sizeof(int));
            if (bytesNeededForEntries > bytesRemaining - minimumBytesForRemainingHeaders)
            {
                entities = new ReadOnlyCollection<DynamicBufferEntityFact>(results);
                return false;
            }

            var entryFacts = new List<DynamicBufferEntryFact>(elementCount);
            for (var entryOrdinal = 0; entryOrdinal < elementCount; entryOrdinal += 1)
            {
                var entryBytes = new byte[entrySize];
                Buffer.BlockCopy(block, offset, entryBytes, 0, entrySize);
                entryFacts.Add(
                    new DynamicBufferEntryFact(
                        entryOrdinal,
                        string.Join(" ", entryBytes.Select(value => value.ToString("X2"))),
                        structuredDecoder(entryBytes)));
                offset += entrySize;
            }

            results.Add(
                new DynamicBufferEntityFact(
                    entityIndexBase + entityOrdinal,
                    archetypeIndex,
                    entityOrdinal,
                    headerValue,
                    elementCount,
                    new ReadOnlyCollection<DynamicBufferEntryFact>(entryFacts)));
        }

        entities = new ReadOnlyCollection<DynamicBufferEntityFact>(results);
        return offset == block.Length;
    }
}

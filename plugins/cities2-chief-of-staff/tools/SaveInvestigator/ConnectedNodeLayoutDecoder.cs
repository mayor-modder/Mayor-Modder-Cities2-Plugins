using System.Collections.ObjectModel;

namespace SaveInvestigator;

internal static class ConnectedNodeLayoutDecoder
{
    private const int HexPreviewBytes = 16;
    private const int Int32PreviewCount = 4;

    public static int? TryGetBytesPerEntity(int blockLength, int entityCount)
    {
        return ArchetypeBufferWalker.TryGetBytesPerEntity(blockLength, entityCount);
    }

    public static ConnectedNodeFlatStrideCandidateFact BuildFlatStrideCandidate(
        byte[] block,
        int entityCount,
        int? bytesPerEntity)
    {
        if (entityCount <= 0 || bytesPerEntity != sizeof(int) + sizeof(float))
        {
            return new ConnectedNodeFlatStrideCandidateFact(
                false,
                bytesPerEntity,
                new ReadOnlyCollection<RailTrackConnectedNodeFact>([]));
        }

        var entries = new List<RailTrackConnectedNodeFact>(entityCount);
        for (var entityOrdinal = 0; entityOrdinal < entityCount; entityOrdinal += 1)
        {
            var offset = entityOrdinal * bytesPerEntity.Value;
            entries.Add(
                new RailTrackConnectedNodeFact(
                    BitConverter.ToInt32(block, offset),
                    BitConverter.ToSingle(block, offset + sizeof(int))));
        }

        return new ConnectedNodeFlatStrideCandidateFact(
            true,
            bytesPerEntity,
            new ReadOnlyCollection<RailTrackConnectedNodeFact>(entries));
    }

    public static ConnectedNodeCountPrefixedCandidateFact BuildCountPrefixedCandidate(byte[] block, int entityCount)
    {
        if (entityCount <= 0)
        {
            return new ConnectedNodeCountPrefixedCandidateFact(
                false,
                0,
                new ReadOnlyCollection<ConnectedNodeCountPrefixedEntityFact>([]));
        }

        var offset = 0;
        var entities = new List<ConnectedNodeCountPrefixedEntityFact>(entityCount);

        for (var entityOrdinal = 0; entityOrdinal < entityCount; entityOrdinal += 1)
        {
            if (offset > block.Length - sizeof(int))
            {
                return new ConnectedNodeCountPrefixedCandidateFact(
                    false,
                    offset,
                    new ReadOnlyCollection<ConnectedNodeCountPrefixedEntityFact>(entities));
            }

            var headerValue = BitConverter.ToInt32(block, offset);
            var candidateCount = headerValue;
            offset += sizeof(int);

            if (candidateCount < 0)
            {
                return new ConnectedNodeCountPrefixedCandidateFact(
                    false,
                    offset,
                    new ReadOnlyCollection<ConnectedNodeCountPrefixedEntityFact>(entities));
            }

            var bytesRemaining = block.Length - offset;
            var bytesNeededForEntries = checked(candidateCount * (sizeof(int) + sizeof(float)));
            var minimumBytesForRemainingHeaders = checked((entityCount - entityOrdinal - 1) * sizeof(int));
            if (bytesNeededForEntries > bytesRemaining - minimumBytesForRemainingHeaders)
            {
                return new ConnectedNodeCountPrefixedCandidateFact(
                    false,
                    offset,
                    new ReadOnlyCollection<ConnectedNodeCountPrefixedEntityFact>(entities));
            }

            var entryDataOffset = offset;
            var entries = new List<RailTrackConnectedNodeFact>(candidateCount);
            for (var entryOrdinal = 0; entryOrdinal < candidateCount; entryOrdinal += 1)
            {
                entries.Add(
                    new RailTrackConnectedNodeFact(
                        BitConverter.ToInt32(block, offset),
                        BitConverter.ToSingle(block, offset + sizeof(int))));
                offset += sizeof(int) + sizeof(float);
            }

            entities.Add(
                new ConnectedNodeCountPrefixedEntityFact(
                    entityOrdinal,
                    candidateCount,
                    headerValue,
                    entryDataOffset,
                    new ReadOnlyCollection<RailTrackConnectedNodeFact>(entries)));
        }

        return new ConnectedNodeCountPrefixedCandidateFact(
            offset == block.Length,
            offset,
            new ReadOnlyCollection<ConnectedNodeCountPrefixedEntityFact>(entities));
    }

    public static string DetermineLikelyLayout(bool flatStrideMatches, bool countPrefixedMatches)
    {
        if (flatStrideMatches && countPrefixedMatches)
        {
            return "ambiguous";
        }

        if (flatStrideMatches)
        {
            return "flat_stride";
        }

        if (countPrefixedMatches)
        {
            return "count_prefixed_buffer";
        }

        return "no_match";
    }

    public static string BuildLeadingHex(byte[] block)
    {
        var previewLength = Math.Min(HexPreviewBytes, block.Length);
        return string.Join(
            " ",
            block
                .Take(previewLength)
                .Select(value => value.ToString("X2")));
    }

    public static List<int> ReadLeadingInt32Values(byte[] block)
    {
        var values = new List<int>();
        var count = Math.Min(Int32PreviewCount, block.Length / sizeof(int));
        for (var index = 0; index < count; index += 1)
        {
            values.Add(BitConverter.ToInt32(block, index * sizeof(int)));
        }

        return values;
    }
}

using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class SystemBufferCatalogFactsExtractor
{
    private const int MinimumAsciiStringLength = 4;
    private const int MaxSampleCount = 12;
    private const int MinEntityLikeIntValue = 1;
    private const int MaxEntityLikeIntValue = 5_000_000;

    public static SystemBufferCatalogFacts Extract(byte[] payload, SavePreludeSummary summary)
    {
        var cursor = ArchetypeBufferWalker.Walk(payload, summary);
        var systems = new List<SystemBufferCatalogSystemFact>(summary.SystemTypes.Count);

        for (var systemIndex = 0; systemIndex < summary.SystemTypes.Count; systemIndex += 1)
        {
            var systemType = summary.SystemTypes[systemIndex];
            var buffer = cursor.ReadNextOuterBufferWithMetadata(summary.BufferFormat);
            var sampleStrings = ExtractReadableStrings(buffer.Data);
            var sampleEntityLikeInts = ExtractEntityLikeInts(buffer.Data);

            systems.Add(
                new SystemBufferCatalogSystemFact(
                    systemIndex,
                    systemType.TypeName,
                    buffer.UncompressedSize,
                    buffer.CompressedSize,
                    ClassifyShape(sampleStrings.Count, sampleEntityLikeInts.Count),
                    sampleStrings.Count,
                    sampleEntityLikeInts.Count,
                    new ReadOnlyCollection<SystemBufferCatalogStringFact>(sampleStrings.Take(MaxSampleCount).ToList()),
                    new ReadOnlyCollection<SystemBufferCatalogEntityLikeIntFact>(sampleEntityLikeInts.Take(MaxSampleCount).ToList())));
        }

        return new SystemBufferCatalogFacts(new ReadOnlyCollection<SystemBufferCatalogSystemFact>(systems));
    }

    private static string ClassifyShape(int readableStringCount, int entityLikeIntCount)
    {
        if (readableStringCount > 0 && entityLikeIntCount > 0)
        {
            return "string_table_like";
        }

        if (readableStringCount == 0 && entityLikeIntCount >= 2)
        {
            return "entity_like_table";
        }

        if (readableStringCount > 0)
        {
            return "string_rich";
        }

        return "opaque";
    }

    private static List<SystemBufferCatalogStringFact> ExtractReadableStrings(byte[] buffer)
    {
        return ReadableStringScanner.Scan(buffer, MinimumAsciiStringLength)
            .Select(item => new SystemBufferCatalogStringFact(item.Encoding, item.Value, item.Offset))
            .ToList();
    }

    private static List<SystemBufferCatalogEntityLikeIntFact> ExtractEntityLikeInts(byte[] buffer)
    {
        var results = new List<SystemBufferCatalogEntityLikeIntFact>();

        for (var offset = 0; offset <= buffer.Length - sizeof(int); offset += sizeof(int))
        {
            var value = BitConverter.ToInt32(buffer, offset);
            if (value < MinEntityLikeIntValue || value > MaxEntityLikeIntValue)
            {
                continue;
            }

            results.Add(new SystemBufferCatalogEntityLikeIntFact(offset, value));
        }

        return results
            .OrderBy(item => item.Offset)
            .ToList();
    }
}

using System.Buffers;
using System.Collections.ObjectModel;
using System.Text;

namespace SaveInvestigator;

public static class SystemTableFactsExtractor
{
    private const string NameSystemTypeName = "Game.UI.NameSystem";
    private const int MaxStringLength = 256;

    public static SystemTableFacts Extract(
        byte[] payload,
        SavePreludeSummary summary,
        SystemBufferCatalogFacts systemBufferCatalogFacts)
    {
        var catalogByIndex = systemBufferCatalogFacts.Systems.ToDictionary(system => system.SystemIndex);
        var cursor = ArchetypeBufferWalker.Walk(payload, summary);
        var reviewedSystems = new List<SystemTableReviewFact>(summary.SystemTypes.Count);
        NameSystemTableFact? nameSystem = null;

        for (var systemIndex = 0; systemIndex < summary.SystemTypes.Count; systemIndex += 1)
        {
            var systemType = summary.SystemTypes[systemIndex];
            var buffer = cursor.ReadNextOuterBufferWithMetadata(summary.BufferFormat);
            catalogByIndex.TryGetValue(systemIndex, out var catalogFact);
            var shapeClassification = catalogFact?.ShapeClassification ?? "uncataloged";

            if (nameSystem is null &&
                systemType.TypeName.StartsWith(NameSystemTypeName, StringComparison.Ordinal) &&
                TryDecodeNameSystemEntries(buffer.Data, out var entries))
            {
                nameSystem = new NameSystemTableFact(
                    systemIndex,
                    systemType.TypeName,
                    buffer.UncompressedSize,
                    buffer.CompressedSize,
                    new ReadOnlyCollection<NameSystemEntryFact>(entries));
                reviewedSystems.Add(
                    new SystemTableReviewFact(
                        systemIndex,
                        systemType.TypeName,
                        shapeClassification,
                        "decoded",
                        "entity_string_table",
                        entries.Count,
                        null));
                continue;
            }

            reviewedSystems.Add(
                new SystemTableReviewFact(
                    systemIndex,
                    systemType.TypeName,
                    shapeClassification,
                    IsTableCandidate(shapeClassification) ? "reviewed_unresolved" : "not_table_candidate",
                    null,
                    null,
                    IsTableCandidate(shapeClassification)
                        ? "Catalog suggests a table-shaped buffer, but the layout is not yet proven."
                        : null));
        }

        return new SystemTableFacts(
            new ReadOnlyCollection<SystemTableReviewFact>(reviewedSystems),
            nameSystem);
    }

    public static bool TryDecodeNameSystemEntries(byte[] buffer, out List<NameSystemEntryFact> entries)
    {
        if (TryDecodeSizedHeaderNameSystemEntries(buffer, out entries) ||
            TryDecodeLegacyNameSystemEntries(buffer, out entries))
        {
            return true;
        }

        entries = [];
        return false;
    }

    private static bool TryDecodeSizedHeaderNameSystemEntries(byte[] buffer, out List<NameSystemEntryFact> entries)
    {
        entries = [];
        if (buffer.Length < sizeof(int) * 2)
        {
            return false;
        }

        var declaredPayloadLength = BitConverter.ToInt32(buffer, 0);
        if (declaredPayloadLength != buffer.Length - sizeof(int))
        {
            return false;
        }

        var entryCount = BitConverter.ToInt32(buffer, sizeof(int));
        return TryDecodeNameSystemEntriesCore(buffer, sizeof(int) * 2, entryCount, out entries);
    }

    private static bool TryDecodeLegacyNameSystemEntries(byte[] buffer, out List<NameSystemEntryFact> entries)
    {
        entries = [];
        if (buffer.Length < sizeof(int))
        {
            return false;
        }

        var entryCount = BitConverter.ToInt32(buffer, 0);
        return TryDecodeNameSystemEntriesCore(buffer, sizeof(int), entryCount, out entries);
    }

    private static bool TryDecodeNameSystemEntriesCore(
        byte[] buffer,
        int offset,
        int entryCount,
        out List<NameSystemEntryFact> entries)
    {
        entries = [];
        if (entryCount < 0 || entryCount > buffer.Length / sizeof(int))
        {
            return false;
        }

        var results = new List<NameSystemEntryFact>(entryCount);
        for (var index = 0; index < entryCount; index += 1)
        {
            if (offset > buffer.Length - sizeof(int))
            {
                return false;
            }

            var entityIndex = BitConverter.ToInt32(buffer, offset);
            offset += sizeof(int);
            var stringOffset = offset;
            if (!TryReadSaveString(buffer, ref offset, out var value))
            {
                return false;
            }

            results.Add(new NameSystemEntryFact(entityIndex, value, stringOffset));
        }

        if (offset != buffer.Length)
        {
            return false;
        }

        entries = results;
        return true;
    }

    private static bool IsTableCandidate(string shapeClassification)
    {
        return string.Equals(shapeClassification, "string_table_like", StringComparison.Ordinal) ||
               string.Equals(shapeClassification, "entity_like_table", StringComparison.Ordinal);
    }

    private static bool TryReadSaveString(byte[] buffer, ref int offset, out string value)
    {
        value = string.Empty;
        if (offset > buffer.Length - sizeof(int))
        {
            return false;
        }

        var characterCount = BitConverter.ToInt32(buffer, offset);
        offset += sizeof(int);
        if (characterCount < 0 || characterCount > MaxStringLength)
        {
            return false;
        }

        var builder = new StringBuilder(characterCount);
        for (var index = 0; index < characterCount; index += 1)
        {
            if (!TryReadUtf8Scalar(buffer, ref offset, out var scalar))
            {
                return false;
            }

            builder.Append(scalar.ToString());
        }

        value = builder.ToString();
        return true;
    }

    private static bool TryReadUtf8Scalar(byte[] buffer, ref int offset, out Rune value)
    {
        value = default;
        if (offset >= buffer.Length)
        {
            return false;
        }

        var status = Rune.DecodeFromUtf8(buffer.AsSpan(offset), out value, out var bytesConsumed);
        if (status != OperationStatus.Done)
        {
            return false;
        }

        offset += bytesConsumed;
        return true;
    }
}

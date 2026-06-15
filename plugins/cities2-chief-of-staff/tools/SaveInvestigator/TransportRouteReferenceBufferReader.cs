using System.Collections.ObjectModel;

namespace SaveInvestigator;

internal sealed record TransportRouteReferenceBufferScanFact(
    int ArchetypeIndex,
    int EntityCount,
    string JoinComponentType,
    int TrailingByteCount,
    int ResolvedEntityCount,
    ReadOnlyCollection<string> EvidenceNotes);

internal sealed record TransportRouteReferenceBufferReadResult(
    IReadOnlyDictionary<(int EntityIndex, string JoinComponentType), List<int>> ReferencesByEntityAndComponent,
    ReadOnlyCollection<TransportRouteReferenceBufferScanFact> BufferFacts);

internal static class TransportRouteReferenceBufferReader
{
    internal const string SubRouteTypeName = "Game.Routes.SubRoute";
    internal const string ConnectedRouteTypeName = "Game.Routes.ConnectedRoute";

    public static TransportRouteReferenceBufferReadResult Extract(
        byte[] payload,
        SavePreludeSummary summary,
        IReadOnlySet<int> knownLineEntityIndexes)
    {
        var result = new Dictionary<(int EntityIndex, string JoinComponentType), List<int>>();
        var routeBufferFacts = new List<TransportRouteReferenceBufferScanFact>();
        var subRouteIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, SubRouteTypeName);
        var connectedRouteIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, ConnectedRouteTypeName);
        var routeComponentIndexes = new Dictionary<int, string>();
        if (subRouteIndex >= 0)
        {
            routeComponentIndexes[subRouteIndex] = SubRouteTypeName;
        }

        if (connectedRouteIndex >= 0)
        {
            routeComponentIndexes[connectedRouteIndex] = ConnectedRouteTypeName;
        }

        var outerCursor = SaveGameDataCursor.AdvanceToArchetypeBuffers(payload, summary.BufferFormat);
        var entityIndexBase = 0;

        foreach (var archetype in summary.Archetypes)
        {
            var routeComponentOrdinals = archetype.ComponentTypeIndexes
                .Select((componentIndex, componentOrdinal) => new { componentIndex, componentOrdinal })
                .Where(item => routeComponentIndexes.ContainsKey(item.componentIndex))
                .ToArray();
            var buffer = outerCursor.ReadNextOuterBuffer(summary.BufferFormat);
            if (routeComponentOrdinals.Length == 0)
            {
                entityIndexBase += archetype.EntityCount;
                continue;
            }

            var archetypeCursor = new SaveGameDataCursor(buffer);
            foreach (var componentIndex in archetype.ComponentTypeIndexes)
            {
                var serializerType = summary.ComponentTypes[componentIndex].SerializerType;
                if (ArchetypeBufferWalker.IsPayloadlessSerializer(serializerType))
                {
                    continue;
                }

                archetypeCursor.ReadBlock();
            }

            var trailingBytes = archetypeCursor.ReadRemainingBytes();
            foreach (var routeComponentOrdinal in routeComponentOrdinals)
            {
                var joinComponentType = routeComponentIndexes[routeComponentOrdinal.componentIndex];
                var resolvedEntityCount = 0;
                if (routeComponentOrdinals.Length == 1 &&
                    trailingBytes.Length > 0 &&
                    TryDecodeEntityReferenceBuffer(
                        trailingBytes,
                        archetype.EntityCount,
                        entityIndexBase,
                        knownLineEntityIndexes,
                        out var refsByEntityIndex))
                {
                    foreach (var pair in refsByEntityIndex)
                    {
                        result[(pair.Key, joinComponentType)] = pair.Value;
                    }

                    resolvedEntityCount = refsByEntityIndex.Count;
                }

                routeBufferFacts.Add(
                    new TransportRouteReferenceBufferScanFact(
                        archetype.Index,
                        archetype.EntityCount,
                        joinComponentType,
                        trailingBytes.Length,
                        resolvedEntityCount,
                        new ReadOnlyCollection<string>(
                            routeComponentOrdinals.Length == 1
                                ? ["decoder:count_prefixed_entity_buffer"]
                                : ["decoder:blocker:multiple_payloadless_route_components"])));
            }

            entityIndexBase += archetype.EntityCount;
        }

        return new TransportRouteReferenceBufferReadResult(
            result,
            new ReadOnlyCollection<TransportRouteReferenceBufferScanFact>(routeBufferFacts));
    }

    private static bool TryDecodeEntityReferenceBuffer(
        byte[] trailingBytes,
        int entityCount,
        int entityIndexBase,
        IReadOnlySet<int> knownLineEntityIndexes,
        out Dictionary<int, List<int>> refsByEntityIndex)
    {
        refsByEntityIndex = new Dictionary<int, List<int>>();
        if (!GenericComponentDecoder.TryDecodeCountPrefixedBuffer(
                trailingBytes,
                entityCount,
                entityIndexBase,
                -1,
                sizeof(int),
                entryBytes => new ReadOnlyCollection<DynamicBufferStructuredValueFact>(
                    [
                        new DynamicBufferStructuredValueFact(
                            "entity_index",
                            BitConverter.ToInt32(entryBytes, 0).ToString())
                    ]),
                out var entities))
        {
            return false;
        }

        foreach (var entity in entities)
        {
            var lineEntityIndexes = entity.Entries
                .Select(entry => BitConverter.ToInt32(ParseHex(entry.RawHex), 0))
                .Where(knownLineEntityIndexes.Contains)
                .Distinct()
                .OrderBy(entityIndex => entityIndex)
                .ToList();
            if (lineEntityIndexes.Count > 0)
            {
                refsByEntityIndex[entity.EntityIndex] = lineEntityIndexes;
            }
        }

        return refsByEntityIndex.Count > 0;
    }

    private static byte[] ParseHex(string rawHex)
    {
        var parts = rawHex.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var bytes = new byte[parts.Length];
        for (var index = 0; index < parts.Length; index += 1)
        {
            bytes[index] = Convert.ToByte(parts[index], 16);
        }

        return bytes;
    }
}

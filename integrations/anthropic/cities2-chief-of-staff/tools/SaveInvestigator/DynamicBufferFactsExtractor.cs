using System.Collections.ObjectModel;
using System.Globalization;

namespace SaveInvestigator;

public static class DynamicBufferFactsExtractor
{
    public static DynamicBufferFacts Extract(
        byte[] payload,
        SavePreludeSummary summary,
        SerializerCatalogFacts serializerCatalogFacts)
    {
        var bufferComponentsByIndex = serializerCatalogFacts.Components
            .Where(component => component.TypeHint.ImplementsBufferElementData)
            .ToDictionary(component => component.ComponentIndex);
        var blocks = new List<DynamicBufferBlockFact>();

        ArchetypeBufferWalker.Walk(
            payload,
            summary,
            onComponentBlock: context =>
            {
                if (!bufferComponentsByIndex.TryGetValue(context.ComponentIndex, out var component))
                {
                    return;
                }

                var entrySize = TryResolveEntrySize(component.TypeName);
                if (!entrySize.HasValue)
                {
                    return;
                }

                if (!GenericComponentDecoder.TryDecodeCountPrefixedBuffer(
                        context.Block,
                        context.ArchetypeContext.Archetype.EntityCount,
                        context.ArchetypeContext.EntityIndexBase,
                        context.ArchetypeContext.Archetype.Index,
                        entrySize.Value,
                        entryBytes => DecodeStructuredValues(component.TypeName, entryBytes),
                        out var entities))
                {
                    return;
                }

                blocks.Add(
                    new DynamicBufferBlockFact(
                        context.ComponentIndex,
                        component.TypeName,
                        component.SerializerType,
                        context.ArchetypeContext.Archetype.Index,
                        context.ArchetypeContext.Archetype.EntityCount,
                        context.BlockLength,
                        entities));
            });

        return new DynamicBufferFacts(
            new ReadOnlyCollection<DynamicBufferBlockFact>(
                blocks
                    .OrderBy(block => block.ComponentIndex)
                    .ThenBy(block => block.ArchetypeIndex)
                    .ToList()));
    }

    private static int? TryResolveEntrySize(string typeName)
    {
        if (typeName.StartsWith("Game.Net.ConnectedNode", StringComparison.Ordinal))
        {
            return sizeof(int) + sizeof(float);
        }

        return null;
    }

    private static ReadOnlyCollection<DynamicBufferStructuredValueFact> DecodeStructuredValues(string typeName, byte[] entryBytes)
    {
        if (typeName.StartsWith("Game.Net.ConnectedNode", StringComparison.Ordinal) &&
            entryBytes.Length == sizeof(int) + sizeof(float))
        {
            return new ReadOnlyCollection<DynamicBufferStructuredValueFact>(
                [
                    new DynamicBufferStructuredValueFact(
                        "node_entity_index",
                        BitConverter.ToInt32(entryBytes, 0).ToString(CultureInfo.InvariantCulture)),
                    new DynamicBufferStructuredValueFact(
                        "curve_position",
                        BitConverter.ToSingle(entryBytes, sizeof(int)).ToString("G9", CultureInfo.InvariantCulture))
                ]);
        }

        return new ReadOnlyCollection<DynamicBufferStructuredValueFact>([]);
    }
}

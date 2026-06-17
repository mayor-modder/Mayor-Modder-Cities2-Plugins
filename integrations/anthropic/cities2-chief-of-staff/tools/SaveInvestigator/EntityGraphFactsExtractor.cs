using System.Collections.ObjectModel;
using System.Globalization;

namespace SaveInvestigator;

public static class EntityGraphFactsExtractor
{
    private const string OwnerTypeName = "Game.Common.Owner";
    private const string AttachedTypeName = "Game.Objects.Attached";
    private const string PrefabRefTypeName = "Game.Prefabs.PrefabRef";
    private static readonly string[] OtherReferenceTypePrefixes =
    [
        "Game.Common.Target",
        "Game.Routes.SubRoute",
        "Game.Net.Edge"
    ];

    public static EntityGraphFacts Extract(
        byte[] payload,
        SavePreludeSummary summary,
        SerializerCatalogFacts serializerCatalogFacts,
        DynamicBufferFacts dynamicBufferFacts)
    {
        var targetArchetypeIndexByEntityIndex = BuildTargetArchetypeIndexByEntityIndex(summary.Archetypes);
        var edgeComponentsByIndex = serializerCatalogFacts.Components
            .Select(
                component => new
                {
                    Component = component,
                    EdgeKind = ClassifyPrimitiveEdgeKind(component.TypeName),
                    DecoderKey = component.BlockShapes
                        .Select(blockShape => GenericComponentDecoderRegistry.CreateKey(
                            component.SerializerType,
                            blockShape.BytesPerEntity,
                            component.TypeHint.ManagedTypeShape))
                        .Distinct()
                        .ToList(),
                    HasDecodableLeadingEntityLane = component.BlockShapes.Any(
                        blockShape => blockShape.BytesPerEntity.HasValue &&
                                      blockShape.BytesPerEntity.Value >= sizeof(int))
                })
            .Where(item => item.EdgeKind is not null &&
                           item.HasDecodableLeadingEntityLane &&
                           item.DecoderKey.Any(key => string.Equals(
                               GenericComponentDecoderRegistry.ResolveDecoderKind(key),
                               "fixed_width_value",
                               StringComparison.Ordinal)))
            .ToDictionary(item => item.Component.ComponentIndex);
        var referenceComponents = edgeComponentsByIndex.Values
            .Select(item => new EntityGraphReferenceComponentFact(item.Component.ComponentIndex, item.Component.TypeName))
            .Concat(
                dynamicBufferFacts.Blocks
                    .Where(block => block.TypeName.StartsWith("Game.Net.ConnectedNode", StringComparison.Ordinal))
                    .Select(block => new EntityGraphReferenceComponentFact(block.ComponentIndex, block.TypeName)))
            .Distinct()
            .OrderBy(component => component.ComponentIndex)
            .ToList();
        var edges = new List<EntityGraphEdgeFact>();

        ArchetypeBufferWalker.Walk(
            payload,
            summary,
            onComponentBlock: context =>
            {
                if (!edgeComponentsByIndex.TryGetValue(context.ComponentIndex, out var item))
                {
                    return;
                }

                var values = Enumerable.Repeat(-1, context.ArchetypeContext.Archetype.EntityCount).ToArray();
                if (!GenericComponentDecoder.TryDecodeLeadingInt32Lane(
                        context.Block,
                        context.ArchetypeContext.Archetype.EntityCount,
                        values))
                {
                    return;
                }

                for (var entityOrdinal = 0; entityOrdinal < values.Length; entityOrdinal += 1)
                {
                    var targetEntityIndex = values[entityOrdinal];
                    if (targetEntityIndex < 0)
                    {
                        continue;
                    }

                    edges.Add(
                        new EntityGraphEdgeFact(
                            item.EdgeKind!,
                            context.ArchetypeContext.EntityIndexBase + entityOrdinal,
                            context.ArchetypeContext.Archetype.Index,
                            entityOrdinal,
                            targetEntityIndex,
                            ResolveTargetArchetypeIndex(targetArchetypeIndexByEntityIndex, targetEntityIndex),
                            context.ComponentIndex));
                }
            });

        foreach (var block in dynamicBufferFacts.Blocks.Where(block => block.TypeName.StartsWith("Game.Net.ConnectedNode", StringComparison.Ordinal)))
        {
            foreach (var entity in block.Entities)
            {
                foreach (var entry in entity.Entries)
                {
                    var nodeField = entry.StructuredValues.FirstOrDefault(value => string.Equals(value.FieldName, "node_entity_index", StringComparison.Ordinal));
                    if (nodeField is null ||
                        !int.TryParse(nodeField.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var targetEntityIndex) ||
                        targetEntityIndex < 0)
                    {
                        continue;
                    }

                    edges.Add(
                        new EntityGraphEdgeFact(
                            "connected_node",
                            entity.EntityIndex,
                            entity.ArchetypeIndex,
                            entity.EntityOrdinal,
                            targetEntityIndex,
                            ResolveTargetArchetypeIndex(targetArchetypeIndexByEntityIndex, targetEntityIndex),
                            block.ComponentIndex));
                }
            }
        }

        var orderedEdges = edges
            .OrderBy(edge => edge.SourceEntityIndex)
            .ThenBy(edge => edge.EdgeKind, StringComparer.Ordinal)
            .ThenBy(edge => edge.TargetEntityIndex)
            .ToList();
        var backlinks = orderedEdges
            .Select((edge, edgeIndex) => new { Edge = edge, EdgeIndex = edgeIndex })
            .GroupBy(item => (item.Edge.TargetEntityIndex, item.Edge.TargetArchetypeIndex))
            .Select(
                group => new EntityGraphBacklinkFact(
                    group.Key.TargetEntityIndex,
                    group.Key.TargetArchetypeIndex,
                    new ReadOnlyCollection<int>(
                        group
                            .OrderBy(item => item.Edge.EdgeKind, StringComparer.Ordinal)
                            .ThenBy(item => item.Edge.SourceEntityIndex)
                            .ThenBy(item => item.Edge.SourceComponentIndex)
                            .Select(item => item.EdgeIndex)
                            .ToList())))
            .OrderBy(backlink => backlink.TargetEntityIndex)
            .ToList();

        return new EntityGraphFacts(
            new ReadOnlyCollection<EntityGraphReferenceComponentFact>(referenceComponents),
            new ReadOnlyCollection<EntityGraphEdgeFact>(orderedEdges),
            new ReadOnlyCollection<EntityGraphBacklinkFact>(backlinks));
    }

    private static string? ClassifyPrimitiveEdgeKind(string typeName)
    {
        if (typeName.StartsWith(OwnerTypeName, StringComparison.Ordinal))
        {
            return "owner";
        }

        if (typeName.StartsWith(AttachedTypeName, StringComparison.Ordinal))
        {
            return "attached";
        }

        if (typeName.StartsWith(PrefabRefTypeName, StringComparison.Ordinal))
        {
            return "prefab";
        }

        if (OtherReferenceTypePrefixes.Any(prefix => typeName.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return "other";
        }

        return null;
    }

    private static int[] BuildTargetArchetypeIndexByEntityIndex(IReadOnlyList<ArchetypeSummary> archetypes)
    {
        var totalEntityCount = archetypes.Sum(archetype => archetype.EntityCount);
        var archetypeIndexByEntityIndex = Enumerable.Repeat(-1, totalEntityCount).ToArray();
        var entityIndexBase = 0;
        foreach (var archetype in archetypes)
        {
            var entityIndexLimit = entityIndexBase + archetype.EntityCount;
            for (var entityIndex = entityIndexBase; entityIndex < entityIndexLimit; entityIndex += 1)
            {
                archetypeIndexByEntityIndex[entityIndex] = archetype.Index;
            }

            entityIndexBase = entityIndexLimit;
        }

        return archetypeIndexByEntityIndex;
    }

    private static int? ResolveTargetArchetypeIndex(int[] targetArchetypeIndexByEntityIndex, int targetEntityIndex)
    {
        if (targetEntityIndex < 0 || targetEntityIndex >= targetArchetypeIndexByEntityIndex.Length)
        {
            return null;
        }

        var archetypeIndex = targetArchetypeIndexByEntityIndex[targetEntityIndex];
        return archetypeIndex >= 0 ? archetypeIndex : null;
    }
}

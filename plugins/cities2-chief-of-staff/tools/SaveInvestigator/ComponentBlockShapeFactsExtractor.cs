using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class ComponentBlockShapeFactsExtractor
{
    public static ComponentBlockShapeFacts Extract(
        byte[] payload,
        SavePreludeSummary summary,
        RailTransitStationFacts railTransitStationFacts,
        TransportStationGraphFacts transportStationGraphFacts)
    {
        var targetTrainStopArchetypes = railTransitStationFacts.StopOwners
            .Where(stop => string.Equals(stop.Mode, "train", StringComparison.Ordinal))
            .Select(stop => stop.StopArchetypeIndex)
            .Where(index => index >= 0)
            .ToHashSet();

        var targetTrainStationArchetypes = transportStationGraphFacts.Stations
            .Where(station => string.Equals(station.Mode, "train", StringComparison.Ordinal))
            .Select(station => station.ArchetypeIndex)
            .Concat(
                transportStationGraphFacts.TrainStopOwnerChains
                    .SelectMany(chain => chain.Chain)
                    .Select(node => node.ArchetypeIndex))
            .Where(index => index >= 0)
            .ToHashSet();
        var targetTrainEdgeArchetypes = railTransitStationFacts.StopOwners
            .Where(stop => string.Equals(stop.Mode, "train", StringComparison.Ordinal) && stop.AttachedEntityIndex >= 0)
            .Select(stop => FindArchetypeIndexForEntity(summary.Archetypes, stop.AttachedEntityIndex))
            .Where(index => index >= 0)
            .ToHashSet();

        var targetArchetypes = targetTrainStopArchetypes
            .Concat(targetTrainStationArchetypes)
            .Concat(targetTrainEdgeArchetypes)
            .ToHashSet();

        if (targetArchetypes.Count == 0)
        {
            return new ComponentBlockShapeFacts(new ReadOnlyCollection<ComponentBlockShapeArchetypeFact>([]));
        }

        var results = new List<ComponentBlockShapeArchetypeFact>();
        var currentComponentFacts = new List<ComponentBlockShapeFact>();
        var currentArchetypeIsTarget = false;

        ArchetypeBufferWalker.Walk(
            payload,
            summary,
            onArchetypeStarted: context =>
            {
                currentArchetypeIsTarget = targetArchetypes.Contains(context.Archetype.Index);
                currentComponentFacts = [];
            },
            onComponentBlock: context =>
            {
                if (!currentArchetypeIsTarget)
                {
                    return;
                }

                currentComponentFacts.Add(
                    new ComponentBlockShapeFact(
                        context.ComponentIndex,
                        context.ComponentOrdinal,
                        context.ComponentType.TypeName,
                        context.ComponentType.SerializerType,
                        context.BlockLength,
                        context.BytesPerEntity,
                        FormatLeadingHex(context.Block, maxByteCount: 16)));
            },
            onArchetypeCompleted: context =>
            {
                if (!currentArchetypeIsTarget)
                {
                    return;
                }

                results.Add(
                    new ComponentBlockShapeArchetypeFact(
                        context.Archetype.Index,
                        context.Archetype.EntityCount,
                        targetTrainStationArchetypes.Contains(context.Archetype.Index),
                        targetTrainStopArchetypes.Contains(context.Archetype.Index),
                        new ReadOnlyCollection<ComponentBlockShapeFact>(currentComponentFacts.ToList())));
            });

        return new ComponentBlockShapeFacts(
            new ReadOnlyCollection<ComponentBlockShapeArchetypeFact>(
                results
                    .OrderBy(item => item.ArchetypeIndex)
                    .ToList()));
    }

    private static int FindArchetypeIndexForEntity(IReadOnlyList<ArchetypeSummary> archetypes, int entityIndex)
    {
        if (entityIndex < 0)
        {
            return -1;
        }

        var entityIndexBase = 0;
        foreach (var archetype in archetypes)
        {
            var entityIndexLimit = entityIndexBase + archetype.EntityCount;
            if (entityIndex >= entityIndexBase && entityIndex < entityIndexLimit)
            {
                return archetype.Index;
            }

            entityIndexBase = entityIndexLimit;
        }

        return -1;
    }

    private static string FormatLeadingHex(byte[] block, int maxByteCount)
    {
        if (block.Length == 0)
        {
            return string.Empty;
        }

        var bytes = block
            .Take(Math.Min(block.Length, maxByteCount))
            .Select(value => value.ToString("X2"))
            .ToArray();
        return string.Join(" ", bytes);
    }
}

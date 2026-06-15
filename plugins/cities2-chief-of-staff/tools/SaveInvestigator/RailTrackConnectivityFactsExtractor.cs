using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class RailTrackConnectivityFactsExtractor
{
    private const string TrainMode = "train";
    private const string OwnerTypeName = "Game.Common.Owner";
    private const string EdgeTypeName = "Game.Net.Edge";
    private const string ConnectedNodeTypeName = "Game.Net.ConnectedNode";

    public static RailTrackConnectivityFacts Extract(
        byte[] payload,
        SavePreludeSummary summary,
        RailTransitStationFacts railTransitStationFacts)
    {
        var attachedTrainStopsByEdgeEntityIndex = railTransitStationFacts.StopOwners
            .Where(stop => string.Equals(stop.Mode, TrainMode, StringComparison.Ordinal) && stop.AttachedEntityIndex >= 0)
            .GroupBy(stop => stop.AttachedEntityIndex)
            .ToDictionary(group => group.Key, group => group.ToArray());
        if (attachedTrainStopsByEdgeEntityIndex.Count == 0)
        {
            return new RailTrackConnectivityFacts(new ReadOnlyCollection<RailTrackConnectivityEdgeFact>([]));
        }

        var ownerIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, OwnerTypeName);
        var edgeIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, EdgeTypeName);
        var connectedNodeIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, ConnectedNodeTypeName);
        if (ownerIndex < 0 || edgeIndex < 0 || connectedNodeIndex < 0)
        {
            return new RailTrackConnectivityFacts(new ReadOnlyCollection<RailTrackConnectivityEdgeFact>([]));
        }

        var edges = new List<RailTrackConnectivityEdgeFact>();
        var currentArchetypeHasTargetComponents = false;
        int[] currentOwnerEntityIndexes = [];
        ReadOnlyCollection<RailTrackConnectedNodeFact>[] currentConnectedNodesByEntityOrdinal = [];

        ArchetypeBufferWalker.Walk(
            payload,
            summary,
            onArchetypeStarted: context =>
            {
                currentArchetypeHasTargetComponents =
                    context.Archetype.ComponentTypeIndexes.Contains(edgeIndex) &&
                    context.Archetype.ComponentTypeIndexes.Contains(connectedNodeIndex);
                currentOwnerEntityIndexes = Enumerable.Repeat(-1, context.Archetype.EntityCount).ToArray();
                currentConnectedNodesByEntityOrdinal = Enumerable
                    .Range(0, context.Archetype.EntityCount)
                    .Select(_ => new ReadOnlyCollection<RailTrackConnectedNodeFact>([]))
                    .ToArray();
            },
            onComponentBlock: context =>
            {
                if (!currentArchetypeHasTargetComponents)
                {
                    return;
                }

                if (context.ComponentIndex == ownerIndex)
                {
                    GenericComponentDecoder.TryDecodeLeadingInt32Lane(
                        context.Block,
                        context.ArchetypeContext.Archetype.EntityCount,
                        currentOwnerEntityIndexes);
                    return;
                }

                if (context.ComponentIndex != connectedNodeIndex)
                {
                    return;
                }

                var candidate = ConnectedNodeLayoutDecoder.BuildCountPrefixedCandidate(
                    context.Block,
                    context.ArchetypeContext.Archetype.EntityCount);
                if (!candidate.MatchesLayout)
                {
                    return;
                }

                foreach (var entityCandidate in candidate.Entities)
                {
                    currentConnectedNodesByEntityOrdinal[entityCandidate.EntityOrdinal] = entityCandidate.Entries;
                }
            },
            onArchetypeCompleted: context =>
            {
                if (!currentArchetypeHasTargetComponents)
                {
                    return;
                }

                for (var entityOrdinal = 0; entityOrdinal < context.Archetype.EntityCount; entityOrdinal += 1)
                {
                    var edgeEntityIndex = context.EntityIndexBase + entityOrdinal;
                    if (!attachedTrainStopsByEdgeEntityIndex.TryGetValue(edgeEntityIndex, out var attachedStops))
                    {
                        continue;
                    }

                    edges.Add(
                        new RailTrackConnectivityEdgeFact(
                            edgeEntityIndex,
                            context.Archetype.Index,
                            entityOrdinal,
                            currentOwnerEntityIndexes[entityOrdinal],
                            GetStartNodeEntityIndex(currentConnectedNodesByEntityOrdinal[entityOrdinal]),
                            GetEndNodeEntityIndex(currentConnectedNodesByEntityOrdinal[entityOrdinal]),
                            new ReadOnlyCollection<int>(
                                attachedStops
                                    .Select(stop => stop.StopEntityIndex)
                                    .OrderBy(entityIndex => entityIndex)
                                    .ToList()),
                            new ReadOnlyCollection<int>(
                                attachedStops
                                    .Select(stop => stop.OwnerEntityIndex)
                                    .Where(entityIndex => entityIndex >= 0)
                                    .Distinct()
                                    .OrderBy(entityIndex => entityIndex)
                                    .ToList()))
                        {
                            ConnectedNodes = currentConnectedNodesByEntityOrdinal[entityOrdinal]
                        });
                }
            });

        return new RailTrackConnectivityFacts(
            new ReadOnlyCollection<RailTrackConnectivityEdgeFact>(
                edges
                    .OrderBy(edge => edge.OwnerEntityIndex)
                    .ThenBy(edge => edge.EdgeEntityIndex)
                    .ToList()));
    }
    private static int GetStartNodeEntityIndex(IReadOnlyList<RailTrackConnectedNodeFact> connectedNodes)
    {
        if (connectedNodes.Count == 0)
        {
            return -1;
        }

        return connectedNodes
            .OrderBy(node => node.CurvePosition)
            .First()
            .NodeEntityIndex;
    }

    private static int GetEndNodeEntityIndex(IReadOnlyList<RailTrackConnectedNodeFact> connectedNodes)
    {
        if (connectedNodes.Count == 0)
        {
            return -1;
        }

        return connectedNodes
            .OrderBy(node => node.CurvePosition)
            .Last()
            .NodeEntityIndex;
    }
}

using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class TransportStationGraphFactsExtractor
{
    private const string OwnerTypeName = "Game.Common.Owner";
    private const string TransportStationTypeName = "Game.Buildings.TransportStation";
    private const string PublicTransportStationTypeName = "Game.Buildings.PublicTransportStation";
    private const string CargoTransportStationTypeName = "Game.Buildings.CargoTransportStation";
    private const string CustomNameTypeName = "Game.UI.CustomName";

    public static TransportStationGraphFacts Extract(
        byte[] payload,
        SavePreludeSummary summary,
        SystemBufferFacts systemBufferFacts,
        RailTransitStationFacts railTransitStationFacts)
    {
        var nameCandidatesByEntityIndex = BuildNameCandidateMap(summary, systemBufferFacts);
        var stations = ExtractStationEntities(summary);
        var trainOwnerSet = railTransitStationFacts.StopOwners
            .Where(stop => string.Equals(stop.Mode, "train", StringComparison.Ordinal))
            .Select(stop => stop.OwnerEntityIndex)
            .Where(entityIndex => entityIndex >= 0)
            .ToHashSet();
        var stationNodesByEntityIndex = stations.ToDictionary(
            station => station.EntityIndex,
            station => CreateStationNode(station, nameCandidatesByEntityIndex));
        var ownerIndex = BuildOwnerIndex(payload, summary, nameCandidatesByEntityIndex);

        var candidateStations = stations
            .Where(station => trainOwnerSet.Contains(station.EntityIndex) && !station.IsCargoStation)
            .ToDictionary(station => station.EntityIndex);
        var ownedChildrenByOwner = BuildOwnedChildren(ownerIndex.NodesByEntityIndex.Values);
        var namedDescendantsByStation = BuildNamedDescendants(candidateStations.Keys, ownedChildrenByOwner);
        var ownerChains = BuildOwnerChains(trainOwnerSet, ownerIndex.OwnerByEntityIndex, ownerIndex.NodesByEntityIndex, stationNodesByEntityIndex);

        var facts = candidateStations.Values
            .OrderBy(station => station.EntityIndex)
            .Select(
                station => new TransportStationGraphFact(
                    "train",
                    station.EntityIndex,
                    station.ArchetypeIndex,
                    station.IsCargoStation,
                    station.HasCustomName,
                    station.HasCustomName && nameCandidatesByEntityIndex.TryGetValue(station.EntityIndex, out var stationName)
                        ? stationName.Value
                        : null,
                    new ReadOnlyCollection<string>(station.ComponentTypes),
                    new ReadOnlyCollection<TransportStationGraphNodeFact>(
                        ownedChildrenByOwner.TryGetValue(station.EntityIndex, out var children)
                            ? children.OrderBy(child => child.EntityIndex).ToList()
                            : []),
                    new ReadOnlyCollection<TransportStationGraphNamedDescendantFact>(
                        namedDescendantsByStation.TryGetValue(station.EntityIndex, out var namedDescendants)
                            ? namedDescendants
                            : [])))
            .ToList();

        return new TransportStationGraphFacts(
            new ReadOnlyCollection<TransportStationGraphFact>(facts),
            new ReadOnlyCollection<TrainStopOwnerChainFact>(ownerChains));
    }

    private static Dictionary<int, NameSystemCandidateNameFact> BuildNameCandidateMap(
        SavePreludeSummary summary,
        SystemBufferFacts systemBufferFacts)
    {
        var customNameIndex = GetComponentIndex(summary.ComponentTypes, CustomNameTypeName);
        if (customNameIndex < 0 || systemBufferFacts.NameSystem is null)
        {
            return [];
        }

        var candidates = systemBufferFacts.NameSystem.CandidateNames;
        var result = new Dictionary<int, NameSystemCandidateNameFact>();
        foreach (var candidate in candidates)
        {
            foreach (var entityIndex in candidate.NearbyEntityIndexes.Where(index => index >= 0))
            {
                if (!result.TryGetValue(entityIndex, out var existing) ||
                    candidate.Value.Length > existing.Value.Length ||
                    (candidate.Value.Length == existing.Value.Length && candidate.StringOffset < existing.StringOffset))
                {
                    result[entityIndex] = candidate;
                }
            }
        }

        return result;
    }

    private static List<StationEntityCandidate> ExtractStationEntities(SavePreludeSummary summary)
    {
        var transportStationIndex = GetComponentIndex(summary.ComponentTypes, TransportStationTypeName);
        var publicTransportStationIndex = GetComponentIndex(summary.ComponentTypes, PublicTransportStationTypeName);
        var cargoTransportStationIndex = GetComponentIndex(summary.ComponentTypes, CargoTransportStationTypeName);
        var customNameIndex = GetComponentIndex(summary.ComponentTypes, CustomNameTypeName);

        var results = new List<StationEntityCandidate>();
        var entityIndexBase = 0;
        foreach (var archetype in summary.Archetypes)
        {
            if (!archetype.ComponentTypeIndexes.Contains(transportStationIndex) ||
                !archetype.ComponentTypeIndexes.Contains(publicTransportStationIndex))
            {
                entityIndexBase += archetype.EntityCount;
                continue;
            }

            var componentTypes = archetype.ComponentTypeIndexes
                .Select(index => summary.ComponentTypes[index].TypeName)
                .ToList();
            var isCargoStation = cargoTransportStationIndex >= 0 && archetype.ComponentTypeIndexes.Contains(cargoTransportStationIndex);
            var hasCustomName = customNameIndex >= 0 && archetype.ComponentTypeIndexes.Contains(customNameIndex);
            for (var entityOrdinal = 0; entityOrdinal < archetype.EntityCount; entityOrdinal += 1)
            {
                results.Add(
                    new StationEntityCandidate(
                        entityIndexBase + entityOrdinal,
                        archetype.Index,
                        isCargoStation,
                        hasCustomName,
                        componentTypes));
            }

            entityIndexBase += archetype.EntityCount;
        }

        return results;
    }

    private static OwnerIndex BuildOwnerIndex(
        byte[] payload,
        SavePreludeSummary summary,
        IReadOnlyDictionary<int, NameSystemCandidateNameFact> nameCandidatesByEntityIndex)
    {
        var ownerIndex = GetComponentIndex(summary.ComponentTypes, OwnerTypeName);
        var customNameIndex = GetComponentIndex(summary.ComponentTypes, CustomNameTypeName);
        if (ownerIndex < 0)
        {
            return new OwnerIndex(new Dictionary<int, int>(), new Dictionary<int, TransportStationGraphNodeFact>());
        }

        var ownerByEntityIndex = new Dictionary<int, int>();
        var nodesByEntityIndex = new Dictionary<int, TransportStationGraphNodeFact>();
        var outerCursor = SaveGameDataCursor.AdvanceToArchetypeBuffers(payload, summary.BufferFormat);
        var entityIndexBase = 0;

        foreach (var archetype in summary.Archetypes)
        {
            var buffer = outerCursor.ReadNextOuterBuffer(summary.BufferFormat);
            if (!archetype.ComponentTypeIndexes.Contains(ownerIndex))
            {
                entityIndexBase += archetype.EntityCount;
                continue;
            }

            var ownerEntityIndexes = Enumerable.Repeat(-1, archetype.EntityCount).ToArray();
            var archetypeCursor = new SaveGameDataCursor(buffer);
            for (var componentOrdinal = 0; componentOrdinal < archetype.ComponentTypeIndexes.Count; componentOrdinal += 1)
            {
                var componentIndex = archetype.ComponentTypeIndexes[componentOrdinal];
                var serializerType = summary.ComponentTypes[componentIndex].SerializerType;
                if (IsSkippedSerializer(serializerType))
                {
                    continue;
                }

                var block = archetypeCursor.ReadBlock();
                if (componentIndex == ownerIndex)
                {
                    TryParseSingleEntityReferenceBlock(block, ownerEntityIndexes);
                }
            }

            var componentTypes = archetype.ComponentTypeIndexes
                .Select(index => summary.ComponentTypes[index].TypeName)
                .ToList();
            var hasCustomName = customNameIndex >= 0 && archetype.ComponentTypeIndexes.Contains(customNameIndex);
            for (var entityOrdinal = 0; entityOrdinal < archetype.EntityCount; entityOrdinal += 1)
            {
                var ownerEntityIndex = ownerEntityIndexes[entityOrdinal];
                var entityIndex = entityIndexBase + entityOrdinal;
                ownerByEntityIndex[entityIndex] = ownerEntityIndex;
                nodesByEntityIndex[entityIndex] = new TransportStationGraphNodeFact(
                    entityIndex,
                    archetype.Index,
                    ownerEntityIndex,
                    hasCustomName,
                    hasCustomName && nameCandidatesByEntityIndex.TryGetValue(entityIndex, out var candidateName)
                        ? candidateName.Value
                        : null,
                    new ReadOnlyCollection<string>(componentTypes));
            }

            entityIndexBase += archetype.EntityCount;
        }

        return new OwnerIndex(ownerByEntityIndex, nodesByEntityIndex);
    }

    private static Dictionary<int, List<TransportStationGraphNodeFact>> BuildOwnedChildren(
        IEnumerable<TransportStationGraphNodeFact> ownerNodes)
    {
        var results = new Dictionary<int, List<TransportStationGraphNodeFact>>();
        foreach (var node in ownerNodes)
        {
            if (node.OwnerEntityIndex < 0)
            {
                continue;
            }

            results.TryAdd(node.OwnerEntityIndex, []);
            results[node.OwnerEntityIndex].Add(node);
        }

        return results;
    }

    private static Dictionary<int, List<TransportStationGraphNamedDescendantFact>> BuildNamedDescendants(
        IEnumerable<int> stationEntityIndexes,
        IReadOnlyDictionary<int, List<TransportStationGraphNodeFact>> ownedChildrenByOwner)
    {
        var results = new Dictionary<int, List<TransportStationGraphNamedDescendantFact>>();

        foreach (var stationEntityIndex in stationEntityIndexes)
        {
            var stationResults = new List<TransportStationGraphNamedDescendantFact>();
            var visited = new HashSet<int> { stationEntityIndex };
            var queue = new Queue<(TransportStationGraphNodeFact Node, List<int> OwnerChain)>();

            if (ownedChildrenByOwner.TryGetValue(stationEntityIndex, out var rootChildren))
            {
                foreach (var child in rootChildren)
                {
                    queue.Enqueue((child, [stationEntityIndex]));
                }
            }

            while (queue.Count > 0)
            {
                var (node, ownerChain) = queue.Dequeue();
                if (!visited.Add(node.EntityIndex))
                {
                    continue;
                }

                if (node.HasCustomName && !string.IsNullOrWhiteSpace(node.MatchedName))
                {
                    stationResults.Add(
                        new TransportStationGraphNamedDescendantFact(
                            node.EntityIndex,
                            node.ArchetypeIndex,
                            node.OwnerEntityIndex,
                            ownerChain.Count,
                            node.MatchedName,
                            new ReadOnlyCollection<int>(ownerChain),
                            node.ComponentTypes));
                }

                if (!ownedChildrenByOwner.TryGetValue(node.EntityIndex, out var descendants))
                {
                    continue;
                }

                foreach (var descendant in descendants)
                {
                    var descendantOwnerChain = new List<int>(ownerChain.Count + 1)
                    {
                        node.EntityIndex
                    };
                    descendantOwnerChain.AddRange(ownerChain);
                    queue.Enqueue((descendant, descendantOwnerChain));
                }
            }

            results[stationEntityIndex] = stationResults
                .OrderBy(descendant => descendant.Depth)
                .ThenBy(descendant => descendant.EntityIndex)
                .ToList();
        }

        return results;
    }

    private static List<TrainStopOwnerChainFact> BuildOwnerChains(
        IReadOnlySet<int> trainOwnerSet,
        IReadOnlyDictionary<int, int> ownerByEntityIndex,
        IReadOnlyDictionary<int, TransportStationGraphNodeFact> nodesByEntityIndex,
        IReadOnlyDictionary<int, TransportStationGraphNodeFact> stationNodesByEntityIndex)
    {
        var results = new List<TrainStopOwnerChainFact>();
        foreach (var start in trainOwnerSet.OrderBy(value => value))
        {
            var chain = new List<TransportStationGraphNodeFact>();
            var seen = new HashSet<int>();
            var current = start;
            while (current >= 0 && seen.Add(current))
            {
                if (nodesByEntityIndex.TryGetValue(current, out var node))
                {
                    chain.Add(node);
                    current = node.OwnerEntityIndex;
                    continue;
                }

                if (stationNodesByEntityIndex.TryGetValue(current, out var stationNode))
                {
                    chain.Add(stationNode);
                }

                break;
            }

            results.Add(
                new TrainStopOwnerChainFact(
                    start,
                    new ReadOnlyCollection<TransportStationGraphNodeFact>(chain)));
        }

        return results;
    }

    private static int GetComponentIndex(IReadOnlyList<ComponentTypeSummary> componentTypes, string requiredPrefix)
    {
        var match = componentTypes.FirstOrDefault(
            component => component.TypeName.StartsWith(requiredPrefix, StringComparison.Ordinal));

        return match?.Index ?? -1;
    }

    private static bool IsSkippedSerializer(byte serializerType)
    {
        return (serializerType & 17) == 1;
    }

    private static bool TryParseSingleEntityReferenceBlock(byte[] block, IList<int> entityIndexes)
    {
        if (block.Length != entityIndexes.Count * sizeof(int))
        {
            return false;
        }

        var cursor = new SaveGameDataCursor(block);
        for (var entityOrdinal = 0; entityOrdinal < entityIndexes.Count; entityOrdinal += 1)
        {
            entityIndexes[entityOrdinal] = cursor.ReadInt32();
        }

        return true;
    }

    private static TransportStationGraphNodeFact CreateStationNode(
        StationEntityCandidate station,
        IReadOnlyDictionary<int, NameSystemCandidateNameFact> nameCandidatesByEntityIndex)
    {
        return new TransportStationGraphNodeFact(
            station.EntityIndex,
            station.ArchetypeIndex,
            -1,
            station.HasCustomName,
            station.HasCustomName && nameCandidatesByEntityIndex.TryGetValue(station.EntityIndex, out var stationName)
                ? stationName.Value
                : null,
            new ReadOnlyCollection<string>(station.ComponentTypes));
    }

    private sealed record StationEntityCandidate(
        int EntityIndex,
        int ArchetypeIndex,
        bool IsCargoStation,
        bool HasCustomName,
        List<string> ComponentTypes);

    private sealed record OwnerIndex(
        Dictionary<int, int> OwnerByEntityIndex,
        Dictionary<int, TransportStationGraphNodeFact> NodesByEntityIndex);
}

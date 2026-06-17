using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class PassengerTrainStationFactsExtractor
{
    private const string TransportStopTypeName = "Game.Routes.TransportStop";
    private const string TrainStopTypeName = "Game.Routes.TrainStop";
    private const string OwnerTypeName = "Game.Common.Owner";
    private const string SubRouteTypeName = "Game.Routes.SubRoute";
    private const string TransportStationTypeName = "Game.Buildings.TransportStation";
    private const string PublicTransportStationTypeName = "Game.Buildings.PublicTransportStation";
    private const string CustomNameTypeName = "Game.UI.CustomName";

    public static PassengerTrainStationFacts Extract(
        byte[] payload,
        SavePreludeSummary summary,
        TargetedSaveFacts targetedFacts,
        SystemBufferFacts systemBufferFacts)
    {
        var stops = ExtractPassengerTrainStops(payload, summary);
        var namedStationBuildings = ExtractNamedStationBuildings(payload, summary);
        var lineByEntityIndex = targetedFacts.TransportLines.ToDictionary(line => line.EntityIndex);
        var lineNameByEntityIndex = BuildLineNameMap(targetedFacts, systemBufferFacts);
        var trainStopOwnerEntityIndexes = stops
            .Select(stop => stop.OwnerEntityIndex)
            .Where(entityIndex => entityIndex >= 0)
            .ToHashSet();
        var stations = BuildStations(
            namedStationBuildings,
            systemBufferFacts,
            lineByEntityIndex,
            lineNameByEntityIndex,
            trainStopOwnerEntityIndexes);

        return new PassengerTrainStationFacts(
            new ReadOnlyCollection<PassengerTrainStationStopFact>(stops),
            new ReadOnlyCollection<PassengerTrainStationFact>(stations));
    }

    private static List<PassengerTrainStationStopFact> ExtractPassengerTrainStops(byte[] payload, SavePreludeSummary summary)
    {
        var transportStopIndex = GetComponentIndex(summary.ComponentTypes, TransportStopTypeName);
        var trainStopIndex = GetComponentIndex(summary.ComponentTypes, TrainStopTypeName);
        var ownerIndex = GetComponentIndex(summary.ComponentTypes, OwnerTypeName);
        if (transportStopIndex < 0 || trainStopIndex < 0 || ownerIndex < 0)
        {
            return [];
        }

        var stops = new List<PassengerTrainStationStopFact>();
        var outerCursor = SaveGameDataCursor.AdvanceToArchetypeBuffers(payload, summary.BufferFormat);
        var entityIndexBase = 0;

        for (var archetypeIndex = 0; archetypeIndex < summary.Archetypes.Count; archetypeIndex += 1)
        {
            var archetype = summary.Archetypes[archetypeIndex];
            var buffer = outerCursor.ReadNextOuterBuffer(summary.BufferFormat);

            if (!archetype.ComponentTypeIndexes.Contains(transportStopIndex) ||
                !archetype.ComponentTypeIndexes.Contains(trainStopIndex))
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
                    ParseSingleEntityReferenceBlock(block, ownerEntityIndexes);
                }
            }

            for (var entityOrdinal = 0; entityOrdinal < archetype.EntityCount; entityOrdinal += 1)
            {
                stops.Add(
                    new PassengerTrainStationStopFact(
                        entityIndexBase + entityOrdinal,
                        archetypeIndex,
                        entityOrdinal,
                        ownerEntityIndexes[entityOrdinal]));
            }

            entityIndexBase += archetype.EntityCount;
        }

        return stops;
    }

    private static List<NamedStationBuildingPartial> ExtractNamedStationBuildings(byte[] payload, SavePreludeSummary summary)
    {
        var subRouteIndex = GetComponentIndex(summary.ComponentTypes, SubRouteTypeName);
        var transportStationIndex = GetComponentIndex(summary.ComponentTypes, TransportStationTypeName);
        var publicTransportStationIndex = GetComponentIndex(summary.ComponentTypes, PublicTransportStationTypeName);
        var customNameIndex = GetComponentIndex(summary.ComponentTypes, CustomNameTypeName);
        var ownerIndex = GetComponentIndex(summary.ComponentTypes, OwnerTypeName);
        if (transportStationIndex < 0 || publicTransportStationIndex < 0)
        {
            return [];
        }

        var buildings = new List<NamedStationBuildingPartial>();
        var outerCursor = SaveGameDataCursor.AdvanceToArchetypeBuffers(payload, summary.BufferFormat);
        var entityIndexBase = 0;

        for (var archetypeIndex = 0; archetypeIndex < summary.Archetypes.Count; archetypeIndex += 1)
        {
            var archetype = summary.Archetypes[archetypeIndex];
            var buffer = outerCursor.ReadNextOuterBuffer(summary.BufferFormat);
            if (!archetype.ComponentTypeIndexes.Contains(transportStationIndex) ||
                !archetype.ComponentTypeIndexes.Contains(publicTransportStationIndex))
            {
                entityIndexBase += archetype.EntityCount;
                continue;
            }

            var subRouteEntityIndexes = Enumerable.Repeat(-1, archetype.EntityCount).ToArray();
            var ownerEntityIndexes = Enumerable.Repeat(-1, archetype.EntityCount).ToArray();
            var hasCustomName = archetype.ComponentTypeIndexes.Contains(customNameIndex);
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
                if (componentIndex == subRouteIndex)
                {
                    ParseSingleEntityReferenceBlock(block, subRouteEntityIndexes);
                    continue;
                }

                if (componentIndex == ownerIndex)
                {
                    ParseSingleEntityReferenceBlock(block, ownerEntityIndexes);
                }
            }

            for (var entityOrdinal = 0; entityOrdinal < archetype.EntityCount; entityOrdinal += 1)
            {
                buildings.Add(
                    new NamedStationBuildingPartial(
                        entityIndexBase + entityOrdinal,
                        archetypeIndex,
                        entityOrdinal,
                        subRouteEntityIndexes[entityOrdinal],
                        ownerEntityIndexes[entityOrdinal],
                        hasCustomName));
            }

            entityIndexBase += archetype.EntityCount;
        }

        return buildings;
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

    private static void ParseSingleEntityReferenceBlock(byte[] block, IList<int> entityIndexes)
    {
        var cursor = new SaveGameDataCursor(block);
        for (var entityOrdinal = 0; entityOrdinal < entityIndexes.Count; entityOrdinal += 1)
        {
            entityIndexes[entityOrdinal] = cursor.ReadInt32();
        }
    }

    private static Dictionary<int, string> BuildLineNameMap(
        TargetedSaveFacts targetedFacts,
        SystemBufferFacts systemBufferFacts)
    {
        var result = new Dictionary<int, string>();
        var matchedLineNames = systemBufferFacts.NameSystem?.MatchedTransportLineNames;
        if (matchedLineNames is null)
        {
            return result;
        }

        foreach (var line in targetedFacts.TransportLines)
        {
            var bestCandidate = matchedLineNames
                .Where(candidate => candidate.NearbyTransportLineEntityIndexes.Contains(line.EntityIndex))
                .OrderByDescending(candidate => candidate.Value.Length)
                .ThenBy(candidate => candidate.StringOffset)
                .FirstOrDefault();

            if (bestCandidate is not null)
            {
                result[line.EntityIndex] = bestCandidate.Value;
            }
        }

        return result;
    }

    private static List<PassengerTrainStationFact> BuildStations(
        IReadOnlyList<NamedStationBuildingPartial> namedStationBuildings,
        SystemBufferFacts systemBufferFacts,
        IReadOnlyDictionary<int, TransportLineFact> lineByEntityIndex,
        IReadOnlyDictionary<int, string> lineNameByEntityIndex,
        IReadOnlySet<int> trainStopOwnerEntityIndexes)
    {
        var candidates = systemBufferFacts.NameSystem?.CandidateNames;
        if (candidates is null || candidates.Count == 0)
        {
            return [];
        }

        var stations = new List<PassengerTrainStationFact>();

        foreach (var building in namedStationBuildings)
        {
            if (!building.HasCustomName)
            {
                continue;
            }

            if (!trainStopOwnerEntityIndexes.Contains(building.EntityIndex) &&
                !trainStopOwnerEntityIndexes.Contains(building.OwnerEntityIndex))
            {
                continue;
            }

            var bestCandidate = candidates
                .Where(candidate => candidate.NearbyEntityIndexes.Contains(building.EntityIndex))
                .OrderByDescending(candidate => candidate.Value.Length)
                .ThenBy(candidate => candidate.StringOffset)
                .FirstOrDefault();
            if (bestCandidate is null)
            {
                continue;
            }

            var servedLines = new List<PassengerTrainStationServedLineFact>();
            var resolvedLineEntityIndex = building.SubRouteEntityIndex;
            var evidenceComponentTypes = new List<string>();
            if (resolvedLineEntityIndex < 0 && building.OwnerEntityIndex >= 0)
            {
                var ownerBuilding = namedStationBuildings.FirstOrDefault(candidate => candidate.EntityIndex == building.OwnerEntityIndex);
                if (ownerBuilding is not null && ownerBuilding.SubRouteEntityIndex >= 0)
                {
                    resolvedLineEntityIndex = ownerBuilding.SubRouteEntityIndex;
                    evidenceComponentTypes.Add(OwnerTypeName + ", Game");
                }
            }

            if (resolvedLineEntityIndex >= 0)
            {
                evidenceComponentTypes.Add(SubRouteTypeName + ", Game");
            }

            if (lineByEntityIndex.TryGetValue(resolvedLineEntityIndex, out var line))
            {
                lineNameByEntityIndex.TryGetValue(line.EntityIndex, out var lineName);
                servedLines.Add(
                    new PassengerTrainStationServedLineFact(
                        line.EntityIndex,
                        line.RouteNumber,
                        line.ColorHex,
                        lineName,
                        new ReadOnlyCollection<string>(evidenceComponentTypes)));
            }

            stations.Add(
                new PassengerTrainStationFact(
                    bestCandidate.Value,
                    building.EntityIndex,
                    bestCandidate.StringOffset,
                    building.OwnerEntityIndex,
                    new ReadOnlyCollection<int>([]),
                    new ReadOnlyCollection<PassengerTrainStationServedLineFact>(servedLines)));
        }

        return stations
            .OrderBy(station => station.Name, StringComparer.Ordinal)
            .ThenBy(station => station.NameEntityIndex)
            .ToList();
    }

    private sealed record NamedStationBuildingPartial(
        int EntityIndex,
        int ArchetypeIndex,
        int EntityOrdinal,
        int SubRouteEntityIndex,
        int OwnerEntityIndex,
        bool HasCustomName);
}

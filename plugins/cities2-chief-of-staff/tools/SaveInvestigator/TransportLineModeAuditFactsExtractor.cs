using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class TransportLineModeAuditFactsExtractor
{
    private const string TransportLineTypeName = "Game.Routes.TransportLine";
    private const string RouteNumberTypeName = "Game.Routes.RouteNumber";
    private const string RouteColorTypeName = "Game.Routes.Color";
    private const string VehicleModelTypeName = "Game.Routes.VehicleModel";
    private const string TransportStopTypeName = "Game.Routes.TransportStop";
    // Stop-side clue used on stop/object entities, not the network-level Game.Net.OutsideConnection component.
    private const string StopObjectOutsideConnectionTypeName = "Game.Objects.OutsideConnection";
    private const string CargoTransportStationTypeName = "Game.Buildings.CargoTransportStation";
    private const string CargoTransportVehicleTypeName = "Game.Prefabs.CargoTransportVehicleData";
    private const string TrainPrefabTypeName = "Game.Prefabs.TrainData";
    private const string WatercraftPrefabTypeName = "Game.Prefabs.WatercraftData";
    private const string CarPrefabTypeName = "Game.Prefabs.CarData";

    private static readonly (string Mode, string TypeName)[] StopFamilyTypeNames =
    [
        ("bus", "Game.Routes.BusStop"),
        ("train", "Game.Routes.TrainStop"),
        ("tram", "Game.Routes.TramStop"),
        ("ship", "Game.Routes.ShipStop"),
        ("ferry", "Game.Routes.FerryStop"),
        ("subway", "Game.Routes.SubwayStop"),
        ("work", "Game.Routes.WorkStop"),
        ("airplane", "Game.Routes.AirplaneStop")
    ];

    public static TransportLineModeAuditFacts Extract(
        byte[] payload,
        SavePreludeSummary summary,
        EntityGraphFacts entityGraphFacts)
    {
        var ownerEntityIndexByEntityIndex = EntityGraphLookup.BuildSingleTargetMap(entityGraphFacts, "owner");
        var ownerArchetypeComponentTypes = BuildEntityComponentTypeLookup(summary);
        var lineFacts = ExtractTransportLines(payload, summary);
        var stopFacts = ExtractStopFacts(payload, summary, ownerEntityIndexByEntityIndex, ownerArchetypeComponentTypes);

        var stopFactsByLineEntityIndex = stopFacts
            .Where(stop => stop.RouteEntityIndex >= 0)
            .GroupBy(stop => stop.RouteEntityIndex)
            .ToDictionary(group => group.Key, group => group.ToList());

        var auditedLines = lineFacts
            .Select(
                line =>
                {
                    stopFactsByLineEntityIndex.TryGetValue(line.EntityIndex, out var stopClues);
                    stopClues ??= [];

                    var clueFacts = stopClues
                        .GroupBy(stop => stop.StopFamily, StringComparer.Ordinal)
                        .Select(
                            group =>
                            {
                                var stopEntityIndexes = group.Select(stop => stop.StopEntityIndex).Distinct().OrderBy(value => value).ToList();
                                var ownerEntityIndexes = group
                                    .Select(stop => stop.OwnerEntityIndex)
                                    .Where(value => value >= 0)
                                    .Distinct()
                                    .OrderBy(value => value)
                                    .ToList();
                                var ownerComponentTypes = group
                                    .SelectMany(stop => stop.OwnerComponentTypes)
                                    .Distinct(StringComparer.Ordinal)
                                    .OrderBy(value => value, StringComparer.Ordinal)
                                    .ToList();
                                return new TransportLineModeAuditStopFamilyClueFact(
                                    group.Key,
                                    group.Count(),
                                    group.Any(stop => stop.HasOutsideConnectionClue),
                                    new ReadOnlyCollection<int>(stopEntityIndexes),
                                    new ReadOnlyCollection<int>(ownerEntityIndexes),
                                    new ReadOnlyCollection<string>(ownerComponentTypes));
                            })
                        .OrderBy(clue => clue.StopFamily, StringComparer.Ordinal)
                        .ToList();
                    var distinctFamilies = clueFacts
                        .Select(clue => clue.StopFamily)
                        .Distinct(StringComparer.Ordinal)
                        .ToList();
                    var vehiclePrefabComponentTypes = line.VehiclePrefabEntityIndex.HasValue
                        ? ownerArchetypeComponentTypes.GetComponentTypes(line.VehiclePrefabEntityIndex.Value)
                            .Distinct(StringComparer.Ordinal)
                            .OrderBy(value => value, StringComparer.Ordinal)
                            .ToList()
                        : [];
                    var vehicleModeClue = ClassifyVehicleModeClue(vehiclePrefabComponentTypes);
                    var hasCargoVehicleClue = vehiclePrefabComponentTypes.Any(
                        componentType => componentType.StartsWith(CargoTransportVehicleTypeName, StringComparison.Ordinal));
                    var candidateMode = distinctFamilies.Count == 1
                        ? distinctFamilies[0]
                        : "unresolved";

                    return new TransportLineModeAuditLineFact(
                        line.EntityIndex,
                        line.ArchetypeIndex,
                        line.RouteNumber,
                        line.ColorHex,
                        candidateMode,
                        vehicleModeClue,
                        line.VehiclePrefabEntityIndex,
                        new ReadOnlyCollection<string>(vehiclePrefabComponentTypes),
                        stopClues.Any(stop => stop.HasCargoOwnerClue) || hasCargoVehicleClue ? true : null,
                        stopClues.Any(stop => stop.HasCargoOwnerClue),
                        stopClues.Any(stop => stop.HasOutsideConnectionClue),
                        new ReadOnlyCollection<TransportLineModeAuditStopFamilyClueFact>(clueFacts));
                })
            .OrderBy(line => line.RouteNumber)
            .ThenBy(line => line.ColorHex, StringComparer.Ordinal)
            .ThenBy(line => line.LineEntityIndex)
            .ToList();

        return new TransportLineModeAuditFacts(new ReadOnlyCollection<TransportLineModeAuditLineFact>(auditedLines));
    }

    private static List<TransportLineAuditSeed> ExtractTransportLines(byte[] payload, SavePreludeSummary summary)
    {
        var transportLineIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, TransportLineTypeName);
        var routeNumberIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, RouteNumberTypeName);
        var routeColorIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, RouteColorTypeName);
        var vehicleModelIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, VehicleModelTypeName);
        var results = new List<TransportLineAuditSeed>();
        byte[]? currentTransportLineBlock = null;
        byte[]? currentRouteNumberBlock = null;
        byte[]? currentRouteColorBlock = null;
        byte[]? currentVehicleModelBlock = null;

        ArchetypeBufferWalker.Walk(
            payload,
            summary,
            onArchetypeStarted: context =>
            {
                currentTransportLineBlock = null;
                currentRouteNumberBlock = null;
                currentRouteColorBlock = null;
                currentVehicleModelBlock = null;
            },
            onComponentBlock: context =>
            {
                if (context.ComponentIndex == transportLineIndex)
                {
                    currentTransportLineBlock = context.Block;
                    return;
                }

                if (context.ComponentIndex == routeNumberIndex)
                {
                    currentRouteNumberBlock = context.Block;
                    return;
                }

                if (context.ComponentIndex == routeColorIndex)
                {
                    currentRouteColorBlock = context.Block;
                    return;
                }

                if (context.ComponentIndex == vehicleModelIndex)
                {
                    currentVehicleModelBlock = context.Block;
                }
            },
            onArchetypeCompleted: context =>
            {
                var built = TransportLineParser.ParseArchetype(
                        context.EntityIndexBase,
                        context.Archetype.Index,
                        context.Archetype.EntityCount,
                        currentTransportLineBlock,
                        currentRouteNumberBlock,
                        currentRouteColorBlock,
                        currentVehicleModelBlock)
                    .Select(
                        line => new TransportLineAuditSeed(
                            line.EntityIndex,
                            line.ArchetypeIndex,
                            line.EntityOrdinal,
                            line.RouteNumber,
                            line.ColorHex,
                            line.VehicleInterval,
                            line.UnbunchingFactor,
                            line.Flags,
                            line.TicketPrice,
                            line.VehicleRequestEntityIndex,
                            line.VehiclePrefabEntityIndex));
                results.AddRange(built);
            });

        return results;
    }

    private static List<StopModeClue> ExtractStopFacts(
        byte[] payload,
        SavePreludeSummary summary,
        IReadOnlyDictionary<int, int> ownerEntityIndexByEntityIndex,
        EntityComponentTypeLookup ownerArchetypeComponentTypes)
    {
        var transportStopIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, TransportStopTypeName);
        var outsideConnectionIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, StopObjectOutsideConnectionTypeName);
        if (transportStopIndex < 0)
        {
            return [];
        }

        var stopFamilyIndexes = StopFamilyTypeNames
            .Select(
                family =>
                {
                    var componentIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, family.TypeName);
                    return (family.Mode, ComponentIndex: componentIndex);
                })
            .Where(family => family.ComponentIndex >= 0)
            .ToArray();
        var results = new List<StopModeClue>();
        var activeStopFamilies = Array.Empty<string>();
        var hasOutsideConnectionClue = false;

        ArchetypeBufferWalker.Walk(
            payload,
            summary,
            onArchetypeStarted: context =>
            {
                activeStopFamilies = stopFamilyIndexes
                    .Where(family => context.Archetype.ComponentTypeIndexes.Contains(family.ComponentIndex))
                    .Select(family => family.Mode)
                    .ToArray();
                hasOutsideConnectionClue = outsideConnectionIndex >= 0 &&
                                           context.Archetype.ComponentTypeIndexes.Contains(outsideConnectionIndex);
            },
            onComponentBlock: context =>
            {
                if (activeStopFamilies.Length == 0 || context.ComponentIndex != transportStopIndex)
                {
                    return;
                }

                if (context.BytesPerEntity is null || context.BytesPerEntity.Value < sizeof(int))
                {
                    return;
                }

                var routeEntityIndexes = new int[context.ArchetypeContext.Archetype.EntityCount];
                if (!GenericComponentDecoder.TryDecodeLeadingInt32Lane(
                        context.Block,
                        context.ArchetypeContext.Archetype.EntityCount,
                        routeEntityIndexes))
                {
                    return;
                }

                for (var entityOrdinal = 0; entityOrdinal < context.ArchetypeContext.Archetype.EntityCount; entityOrdinal += 1)
                {
                    var entityIndex = context.ArchetypeContext.EntityIndexBase + entityOrdinal;
                    var routeEntityIndex = routeEntityIndexes[entityOrdinal];

                    var ownerEntityIndex = ownerEntityIndexByEntityIndex.TryGetValue(entityIndex, out var matchedOwnerEntityIndex)
                        ? matchedOwnerEntityIndex
                        : -1;
                    var ownerComponentTypes = BuildOwnerComponentTypeTrail(
                        ownerEntityIndex,
                        ownerEntityIndexByEntityIndex,
                        ownerArchetypeComponentTypes);
                    var hasCargoOwnerClue = ownerComponentTypes.Any(
                        componentType => componentType.StartsWith(CargoTransportStationTypeName, StringComparison.Ordinal));

                    foreach (var stopFamily in activeStopFamilies)
                    {
                        results.Add(
                            new StopModeClue(
                                entityIndex,
                                routeEntityIndex,
                                stopFamily,
                                ownerEntityIndex,
                                hasOutsideConnectionClue,
                                hasCargoOwnerClue,
                                new ReadOnlyCollection<string>(ownerComponentTypes)));
                    }
                }
            });

        return results;
    }

    private static List<string> BuildOwnerComponentTypeTrail(
        int ownerEntityIndex,
        IReadOnlyDictionary<int, int> ownerEntityIndexByEntityIndex,
        EntityComponentTypeLookup ownerArchetypeComponentTypes)
    {
        var results = new List<string>();
        var seenEntities = new HashSet<int>();
        var seenTypes = new HashSet<string>(StringComparer.Ordinal);
        var currentEntityIndex = ownerEntityIndex;

        while (currentEntityIndex >= 0 && seenEntities.Add(currentEntityIndex))
        {
            foreach (var componentType in ownerArchetypeComponentTypes.GetComponentTypes(currentEntityIndex))
            {
                if (seenTypes.Add(componentType))
                {
                    results.Add(componentType);
                }
            }

            if (!ownerEntityIndexByEntityIndex.TryGetValue(currentEntityIndex, out currentEntityIndex))
            {
                break;
            }
        }

        return results;
    }

    private static EntityComponentTypeLookup BuildEntityComponentTypeLookup(SavePreludeSummary summary)
    {
        var ranges = new List<EntityArchetypeRange>(summary.Archetypes.Count);
        var entityIndexBase = 0;
        foreach (var archetype in summary.Archetypes)
        {
            var componentTypes = archetype.ComponentTypeIndexes
                .Select(index => summary.ComponentTypes[index].TypeName)
                .ToArray();
            ranges.Add(new EntityArchetypeRange(entityIndexBase, entityIndexBase + archetype.EntityCount - 1, componentTypes));
            entityIndexBase += archetype.EntityCount;
        }

        return new EntityComponentTypeLookup(new ReadOnlyCollection<EntityArchetypeRange>(ranges));
    }

    private static string? ClassifyVehicleModeClue(IReadOnlyList<string> vehiclePrefabComponentTypes)
    {
        if (vehiclePrefabComponentTypes.Any(componentType => componentType.StartsWith(TrainPrefabTypeName, StringComparison.Ordinal)))
        {
            return "rail_vehicle";
        }

        if (vehiclePrefabComponentTypes.Any(componentType => componentType.StartsWith(WatercraftPrefabTypeName, StringComparison.Ordinal)))
        {
            return "watercraft";
        }

        if (vehiclePrefabComponentTypes.Any(componentType => componentType.StartsWith(CarPrefabTypeName, StringComparison.Ordinal)))
        {
            return "road_vehicle";
        }

        return null;
    }

    private sealed record StopModeClue(
        int StopEntityIndex,
        int RouteEntityIndex,
        string StopFamily,
        int OwnerEntityIndex,
        bool HasOutsideConnectionClue,
        bool HasCargoOwnerClue,
        ReadOnlyCollection<string> OwnerComponentTypes);

    private sealed record TransportLineAuditSeed(
        int EntityIndex,
        int ArchetypeIndex,
        int EntityOrdinal,
        int RouteNumber,
        string ColorHex,
        float VehicleInterval,
        float UnbunchingFactor,
        ushort Flags,
        ushort TicketPrice,
        int VehicleRequestEntityIndex,
        int? VehiclePrefabEntityIndex);

    private sealed record EntityArchetypeRange(
        int StartEntityIndex,
        int EndEntityIndex,
        IReadOnlyList<string> ComponentTypes);

    private sealed class EntityComponentTypeLookup(ReadOnlyCollection<EntityArchetypeRange> ranges)
    {
        private readonly ReadOnlyCollection<EntityArchetypeRange> _ranges = ranges;

        public IReadOnlyList<string> GetComponentTypes(int entityIndex)
        {
            if (entityIndex < 0)
            {
                return [];
            }

            var low = 0;
            var high = _ranges.Count - 1;
            while (low <= high)
            {
                var mid = low + ((high - low) / 2);
                var range = _ranges[mid];
                if (entityIndex < range.StartEntityIndex)
                {
                    high = mid - 1;
                    continue;
                }

                if (entityIndex > range.EndEntityIndex)
                {
                    low = mid + 1;
                    continue;
                }

                return range.ComponentTypes;
            }

            return [];
        }
    }
}

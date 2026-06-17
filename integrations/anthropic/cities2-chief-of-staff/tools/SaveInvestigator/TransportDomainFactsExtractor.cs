using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class TransportDomainFactsExtractor
{
    private const string TransportLineTypeName = "Game.Routes.TransportLine";
    private const string RouteNumberTypeName = "Game.Routes.RouteNumber";
    private const string RouteColorTypeName = "Game.Routes.Color";
    private const string WaitingPassengersTypeName = "Game.Routes.WaitingPassengers";
    // Network/component-side outside-connection data, distinct from the stop/object tag below.
    private const string NetworkOutsideConnectionTypeName = "Game.Net.OutsideConnection";
    private const string TransportStopTypeName = "Game.Routes.TransportStop";
    private const string TrainStopTypeName = "Game.Routes.TrainStop";
    private const string SubwayStopTypeName = "Game.Routes.SubwayStop";
    private const string OwnerTypeName = "Game.Common.Owner";
    private const string AttachedTypeName = "Game.Objects.Attached";
    private const string TransportStationTypeName = "Game.Buildings.TransportStation";
    private const string PublicTransportStationTypeName = "Game.Buildings.PublicTransportStation";
    private const string CustomNameTypeName = "Game.UI.CustomName";
    // Stop/object-side clue used to identify stations and stops associated with external terminals.
    private const string StopObjectOutsideConnectionTypeName = "Game.Objects.OutsideConnection";
    private const string BuildingSubNetTypeName = "Game.Net.SubNet";
    private const string BuildingSubLaneTypeName = "Game.Net.SubLane";

    public static TransportDomainFacts Extract(
        byte[] payload,
        SavePreludeSummary summary,
        SystemTableFacts systemTableFacts,
        EntityGraphFacts entityGraphFacts)
    {
        var transportLineModeAuditFacts = TransportLineModeAuditFactsExtractor.Extract(
            payload,
            summary,
            entityGraphFacts);
        return Extract(
            payload,
            summary,
            systemTableFacts,
            entityGraphFacts,
            transportLineModeAuditFacts);
    }

    public static TransportDomainFacts Extract(
        byte[] payload,
        SavePreludeSummary summary,
        SystemTableFacts systemTableFacts,
        EntityGraphFacts entityGraphFacts,
        TransportLineModeAuditFacts transportLineModeAuditFacts)
    {
        var ownerEntityIndexByEntityIndex = EntityGraphLookup.BuildSingleTargetMap(entityGraphFacts, "owner");
        var attachedEntityIndexByEntityIndex = EntityGraphLookup.BuildSingleTargetMap(entityGraphFacts, "attached");

        var transportLineFacts = ExtractTransportLineFacts(payload, summary, transportLineModeAuditFacts);
        var lineByEntityIndex = transportLineFacts.TransportLines.ToDictionary(line => line.EntityIndex);
        var waitingPassengersStops = ExtractWaitingPassengers(
            payload,
            summary,
            ownerEntityIndexByEntityIndex);
        var waitingPassengersSummary = new WaitingPassengersSummary(
            waitingPassengersStops.Sum(stop => stop.Count),
            waitingPassengersStops.Count == 0 ? 0 : waitingPassengersStops.Max(stop => stop.Count),
            new ReadOnlyCollection<WaitingPassengersStopFact>(waitingPassengersStops));
        var lineQueues = waitingPassengersStops
            .Where(stop => lineByEntityIndex.ContainsKey(stop.OwnerEntityIndex))
            .GroupBy(stop => stop.OwnerEntityIndex)
            .Select(
                group =>
                {
                    var line = lineByEntityIndex[group.Key];
                    var topStop = group
                        .OrderByDescending(stop => stop.Count)
                        .ThenBy(stop => stop.EntityIndex)
                        .First();
                    return new TransportLineQueueFact(
                        line.EntityIndex,
                        line.RouteNumber,
                        line.ColorHex,
                        group.Count(),
                        group.Sum(stop => stop.Count),
                        topStop.Count,
                        topStop.EntityIndex,
                        topStop.OwnerEntityIndex);
                })
            .OrderByDescending(line => line.TotalWaitingPassengers)
            .ThenByDescending(line => line.MaxStopQueue)
            .ThenBy(line => line.RouteNumber)
            .ThenBy(line => line.ColorHex, StringComparer.Ordinal)
            .ToList();
        var outsideConnections = ExtractOutsideConnections(payload, summary);

        var trainStops = ExtractStops(summary, TrainStopTypeName, "train", ownerEntityIndexByEntityIndex, attachedEntityIndexByEntityIndex);
        var subwayStops = ExtractStops(summary, SubwayStopTypeName, "subway", ownerEntityIndexByEntityIndex, attachedEntityIndexByEntityIndex);
        var buildingCandidates = ExtractBuildingCandidates(summary, ownerEntityIndexByEntityIndex);
        var buildingByEntityIndex = buildingCandidates.ToDictionary(candidate => candidate.EntityIndex);
        var nameByEntityIndex = NameSystemLookup.BuildEntryByEntityIndex(systemTableFacts);
        var stopOwners = trainStops
            .Concat(subwayStops)
            .Select(
                stop => new RailTransitStopOwnerFact(
                    stop.Mode,
                    stop.StopEntityIndex,
                    stop.StopArchetypeIndex,
                    stop.StopEntityOrdinal,
                    stop.OwnerEntityIndex,
                    stop.AttachedEntityIndex,
                    stop.IsOutsideConnection))
            .OrderBy(stop => stop.Mode, StringComparer.Ordinal)
            .ThenBy(stop => stop.OwnerEntityIndex)
            .ThenBy(stop => stop.StopEntityIndex)
            .ToList();
        var stations = BuildStations(
            trainStops,
            subwayStops,
            buildingCandidates,
            buildingByEntityIndex,
            nameByEntityIndex,
            ownerEntityIndexByEntityIndex);

        return new TransportDomainFacts(
            new ReadOnlyCollection<TransportLineFact>(transportLineFacts.TransportLines),
            waitingPassengersSummary,
            new ReadOnlyCollection<OutsideConnectionFact>(outsideConnections),
            new ReadOnlyCollection<TransportLineQueueFact>(lineQueues),
            new ReadOnlyCollection<RailTransitStopOwnerFact>(stopOwners),
            new ReadOnlyCollection<RailTransitStationFact>(stations),
            new ReadOnlyCollection<TransportFacilityFact>([]));
    }

    public static TransportDomainFacts ApplyTransportFacilityClassificationAudit(
        TransportDomainFacts transportDomainFacts,
        TransportFacilityClassificationAuditFacts transportFacilityClassificationAuditFacts)
    {
        var facilities = transportFacilityClassificationAuditFacts.Facilities
            .Select(
                facility => new TransportFacilityFact(
                    facility.Name,
                    facility.NameEntityIndex,
                    facility.BaseFacilityEntityIndex,
                    facility.CandidateRole,
                    facility.CandidateMode,
                    facility.ClassificationStatus,
                    facility.RelatedEntityIndexes,
                    facility.EvidenceNotes))
            .OrderBy(facility => facility.Mode, StringComparer.Ordinal)
            .ThenBy(facility => facility.BaseFacilityEntityIndex)
            .ThenBy(facility => facility.Role, StringComparer.Ordinal)
            .ThenBy(facility => facility.Name, StringComparer.Ordinal)
            .ThenBy(facility => facility.NameEntityIndex)
            .ToList();

        return transportDomainFacts with
        {
            TransportFacilities = new ReadOnlyCollection<TransportFacilityFact>(facilities)
        };
    }

    public static TransportDomainFacts ApplyStationServiceAudit(
        TransportDomainFacts transportDomainFacts,
        TransportStationServiceAuditFacts transportStationServiceAuditFacts)
    {
        var auditByKey = transportStationServiceAuditFacts.Stations.ToDictionary(CreateStationAuditKey);
        var enrichedStations = transportDomainFacts.RailTransitStations
            .Select(
                station =>
                {
                    if (!auditByKey.TryGetValue(CreateStationAuditKey(station), out var auditStation))
                    {
                        return station;
                    }

                    var resolvedLines = string.Equals(auditStation.JoinStatus, "resolved", StringComparison.Ordinal)
                        ? ConvertAuditLines(auditStation.CandidateLines)
                        : station.ServedLines;
                    var candidateLines = string.Equals(auditStation.JoinStatus, "candidate_only", StringComparison.Ordinal)
                        ? ConvertAuditLines(auditStation.CandidateLines)
                        : new ReadOnlyCollection<RailTransitServedLineFact>([]);

                    return station with
                    {
                        ServedLines = resolvedLines,
                        ServiceJoinStatus = auditStation.JoinStatus,
                        CandidateLines = candidateLines
                    };
                })
            .ToList();

        return transportDomainFacts with
        {
            RailTransitStations = new ReadOnlyCollection<RailTransitStationFact>(enrichedStations)
        };
    }

    public static TransportDomainFacts ApplyRemainingRailIdentityAudit(
        TransportDomainFacts transportDomainFacts,
        RemainingRailIdentityFacts remainingRailIdentityFacts)
    {
        var promotedModesByEntityIndex = remainingRailIdentityFacts.Lines
            .Where(CanPromoteExactMode)
            .ToDictionary(line => line.LineEntityIndex, _ => "subway");

        if (promotedModesByEntityIndex.Count == 0)
        {
            return transportDomainFacts;
        }

        var updatedLines = transportDomainFacts.TransportLines
            .Select(
                line =>
                {
                    if (!promotedModesByEntityIndex.TryGetValue(line.EntityIndex, out var mode) ||
                        !string.Equals(line.Mode, "unresolved", StringComparison.Ordinal))
                    {
                        return line;
                    }

                    var notes = new List<string>(line.ModeEvidenceNotes ?? new ReadOnlyCollection<string>([]))
                    {
                        $"remaining_identity_audit:{mode}_by_exclusion"
                    };

                    return line with
                    {
                        Mode = mode,
                        ModeEvidenceNotes = new ReadOnlyCollection<string>(notes)
                    };
                })
            .ToList();

        return transportDomainFacts with
        {
            TransportLines = new ReadOnlyCollection<TransportLineFact>(updatedLines)
        };
    }

    public static TransportDomainFacts ApplyTransportServiceJoinFacts(
        TransportDomainFacts transportDomainFacts,
        TransportServiceJoinFacts transportServiceJoinFacts)
    {
        var joinByKey = transportServiceJoinFacts.Stations.ToDictionary(CreateStationJoinKey);
        var enrichedStations = transportDomainFacts.RailTransitStations
            .Select(
                station =>
                {
                    if (!joinByKey.TryGetValue(CreateStationJoinKey(station), out var joinStation) ||
                        !string.Equals(joinStation.JoinStatus, "resolved", StringComparison.Ordinal))
                    {
                        return station;
                    }

                    return station with
                    {
                        ServedLines = ConvertJoinLines(joinStation.ExactLines),
                        ServiceJoinStatus = "resolved",
                        CandidateLines = new ReadOnlyCollection<RailTransitServedLineFact>([])
                    };
                })
            .ToList();

        return transportDomainFacts with
        {
            RailTransitStations = new ReadOnlyCollection<RailTransitStationFact>(enrichedStations)
        };
    }

    public static TransportDomainFacts ApplyFacilityBackedLineIdentity(TransportDomainFacts transportDomainFacts)
    {
        var resolvedFacilityModesByLineEntityIndex = BuildResolvedFacilityModesByLineEntityIndex(transportDomainFacts);
        var updatedLines = transportDomainFacts.TransportLines
            .Select(
                line =>
                {
                    if (!string.Equals(line.Mode, "unresolved", StringComparison.Ordinal))
                    {
                        return line;
                    }

                    if (line.IsCargo == true && HasModeEvidence(line, "vehicle_family:rail_vehicle"))
                    {
                        return PromoteLineMode(line, "cargo_train", "facility_identity:cargo_rail_vehicle");
                    }

                    if (!resolvedFacilityModesByLineEntityIndex.TryGetValue(line.EntityIndex, out var facilityModes) ||
                        facilityModes.Count != 1)
                    {
                        return line;
                    }

                    var facilityMode = facilityModes.Single();
                    return PromoteLineMode(line, facilityMode, $"facility_identity:{facilityMode}_service");
                })
            .ToList();

        return transportDomainFacts with
        {
            TransportLines = new ReadOnlyCollection<TransportLineFact>(updatedLines)
        };
    }

    [Obsolete("Compatibility shim. Prefer extracting TransportDomainFacts once and passing the precomputed graph/system tables through the newer overloads.")]
    public static EntityGraphFacts BuildCompatibilityEntityGraph(byte[] payload, SavePreludeSummary summary)
    {
        var serializerCatalogFacts = SerializerCatalogFactsExtractor.Extract(
            payload,
            summary,
            static _ => new SerializerCatalogTypeHint(false, null, null, "unknown", false));
        return EntityGraphFactsExtractor.Extract(
            payload,
            summary,
            serializerCatalogFacts,
            new DynamicBufferFacts(new ReadOnlyCollection<DynamicBufferBlockFact>([])));
    }

    private static bool CanPromoteExactMode(RemainingRailIdentityLineFact line)
    {
        return line.CandidateModes.Count == 1 &&
               line.ExclusionNotes.Contains("exact:facility_backed_identity", StringComparer.Ordinal);
    }

    private static TransportLineExtractionResult ExtractTransportLineFacts(
        byte[] payload,
        SavePreludeSummary summary,
        TransportLineModeAuditFacts transportLineModeAuditFacts)
    {
        var transportLineIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, TransportLineTypeName);
        var routeNumberIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, RouteNumberTypeName);
        var routeColorIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, RouteColorTypeName);
        var modeAuditByEntityIndex = transportLineModeAuditFacts.Lines.ToDictionary(line => line.LineEntityIndex);

        var transportLines = new List<TransportLineFact>();
        byte[]? currentTransportLineBlock = null;
        byte[]? currentRouteNumberBlock = null;
        byte[]? currentRouteColorBlock = null;

        ArchetypeBufferWalker.Walk(
            payload,
            summary,
            onArchetypeStarted: context =>
            {
                currentTransportLineBlock = null;
                currentRouteNumberBlock = null;
                currentRouteColorBlock = null;
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
                }
            },
            onArchetypeCompleted: context =>
            {
                var builtLines = TransportLineParser.ParseArchetype(
                        context.EntityIndexBase,
                        context.Archetype.Index,
                        context.Archetype.EntityCount,
                        currentTransportLineBlock,
                        currentRouteNumberBlock,
                        currentRouteColorBlock)
                    .Select(
                        line =>
                        {
                            var builtLine = new TransportLineFact(
                                line.EntityIndex,
                                line.ArchetypeIndex,
                                line.EntityOrdinal,
                                line.RouteNumber,
                                line.ColorHex,
                                line.VehicleInterval,
                                line.UnbunchingFactor,
                                line.Flags,
                                line.TicketPrice,
                                line.VehicleRequestEntityIndex);
                            modeAuditByEntityIndex.TryGetValue(builtLine.EntityIndex, out var modeAudit);
                            return ApplyModeAudit(builtLine, modeAudit);
                        })
                    .ToArray();
                transportLines.AddRange(builtLines);
            });

        return new TransportLineExtractionResult(
            transportLines
                .OrderBy(line => line.EntityIndex)
                .ToList());
    }

    private static List<WaitingPassengersStopFact> ExtractWaitingPassengers(
        byte[] payload,
        SavePreludeSummary summary,
        IReadOnlyDictionary<int, int> ownerEntityIndexByEntityIndex)
    {
        var waitingPassengersIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, WaitingPassengersTypeName);
        if (waitingPassengersIndex < 0)
        {
            return [];
        }

        var results = new List<WaitingPassengersStopFact>();
        ArchetypeBufferWalker.Walk(
            payload,
            summary,
            onComponentBlock: context =>
            {
                if (context.ComponentIndex != waitingPassengersIndex)
                {
                    return;
                }

                var cursor = new SaveGameDataCursor(context.Block);
                for (var entityOrdinal = 0; entityOrdinal < context.ArchetypeContext.Archetype.EntityCount; entityOrdinal += 1)
                {
                    var entityIndex = context.ArchetypeContext.EntityIndexBase + entityOrdinal;
                    results.Add(
                        new WaitingPassengersStopFact(
                            entityIndex,
                            context.ArchetypeContext.Archetype.Index,
                            entityOrdinal,
                            ownerEntityIndexByEntityIndex.TryGetValue(entityIndex, out var ownerEntityIndex)
                                ? ownerEntityIndex
                                : -1,
                            cursor.ReadInt32(),
                            cursor.ReadInt32(),
                            cursor.ReadInt32(),
                            cursor.ReadUInt16(),
                            cursor.ReadUInt16()));
                }
            });

        return results;
    }

    private static List<OutsideConnectionFact> ExtractOutsideConnections(byte[] payload, SavePreludeSummary summary)
    {
        var outsideConnectionIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, NetworkOutsideConnectionTypeName);
        if (outsideConnectionIndex < 0)
        {
            return [];
        }

        var results = new List<OutsideConnectionFact>();
        ArchetypeBufferWalker.Walk(
            payload,
            summary,
            onComponentBlock: context =>
            {
                if (context.ComponentIndex != outsideConnectionIndex)
                {
                    return;
                }

                var cursor = new SaveGameDataCursor(context.Block);
                for (var entityOrdinal = 0; entityOrdinal < context.ArchetypeContext.Archetype.EntityCount; entityOrdinal += 1)
                {
                    results.Add(
                        new OutsideConnectionFact(
                            context.ArchetypeContext.Archetype.Index,
                            entityOrdinal,
                            cursor.ReadSingle()));
                }
            });

        return results;
    }

    private static List<StopCandidate> ExtractStops(
        SavePreludeSummary summary,
        string specificStopTypeName,
        string mode,
        IReadOnlyDictionary<int, int> ownerEntityIndexByEntityIndex,
        IReadOnlyDictionary<int, int> attachedEntityIndexByEntityIndex)
    {
        var transportStopIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, TransportStopTypeName);
        var specificStopIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, specificStopTypeName);
        var customNameIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, CustomNameTypeName);
        var outsideConnectionIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, StopObjectOutsideConnectionTypeName);
        if (transportStopIndex < 0 || specificStopIndex < 0)
        {
            return [];
        }

        var results = new List<StopCandidate>();
        var entityIndexBase = 0;
        foreach (var archetype in summary.Archetypes)
        {
            var isMatchingStopFamily = archetype.ComponentTypeIndexes.Contains(transportStopIndex) &&
                                       archetype.ComponentTypeIndexes.Contains(specificStopIndex);
            if (!isMatchingStopFamily)
            {
                entityIndexBase += archetype.EntityCount;
                continue;
            }

            var hasCustomName = customNameIndex >= 0 && archetype.ComponentTypeIndexes.Contains(customNameIndex);
            var isOutsideConnection = outsideConnectionIndex >= 0 && archetype.ComponentTypeIndexes.Contains(outsideConnectionIndex);
            for (var entityOrdinal = 0; entityOrdinal < archetype.EntityCount; entityOrdinal += 1)
            {
                var entityIndex = entityIndexBase + entityOrdinal;
                results.Add(
                    new StopCandidate(
                        mode,
                        entityIndex,
                        archetype.Index,
                        entityOrdinal,
                        ownerEntityIndexByEntityIndex.TryGetValue(entityIndex, out var ownerEntityIndex)
                            ? ownerEntityIndex
                            : -1,
                        attachedEntityIndexByEntityIndex.TryGetValue(entityIndex, out var attachedEntityIndex)
                            ? attachedEntityIndex
                            : -1,
                        hasCustomName,
                        isOutsideConnection));
            }

            entityIndexBase += archetype.EntityCount;
        }

        return results;
    }

    private static List<StationBuildingCandidate> ExtractBuildingCandidates(
        SavePreludeSummary summary,
        IReadOnlyDictionary<int, int> ownerEntityIndexByEntityIndex)
    {
        var transportStationIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, TransportStationTypeName);
        var publicTransportStationIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, PublicTransportStationTypeName);
        var customNameIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, CustomNameTypeName);
        var subNetIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, BuildingSubNetTypeName);
        var subLaneIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, BuildingSubLaneTypeName);
        if (transportStationIndex < 0 || publicTransportStationIndex < 0)
        {
            return [];
        }

        var results = new List<StationBuildingCandidate>();
        var entityIndexBase = 0;
        foreach (var archetype in summary.Archetypes)
        {
            var isMatchingBuildingFamily = archetype.ComponentTypeIndexes.Contains(transportStationIndex) &&
                                           archetype.ComponentTypeIndexes.Contains(publicTransportStationIndex);
            if (!isMatchingBuildingFamily)
            {
                entityIndexBase += archetype.EntityCount;
                continue;
            }

            var hasCustomName = customNameIndex >= 0 && archetype.ComponentTypeIndexes.Contains(customNameIndex);
            var hasSubNet = subNetIndex >= 0 && archetype.ComponentTypeIndexes.Contains(subNetIndex);
            var hasSubLane = subLaneIndex >= 0 && archetype.ComponentTypeIndexes.Contains(subLaneIndex);
            for (var entityOrdinal = 0; entityOrdinal < archetype.EntityCount; entityOrdinal += 1)
            {
                var entityIndex = entityIndexBase + entityOrdinal;
                results.Add(
                    new StationBuildingCandidate(
                        entityIndex,
                        archetype.Index,
                        entityOrdinal,
                        ownerEntityIndexByEntityIndex.TryGetValue(entityIndex, out var ownerEntityIndex)
                            ? ownerEntityIndex
                            : -1,
                        hasSubNet,
                        hasSubLane,
                        hasCustomName));
            }

            entityIndexBase += archetype.EntityCount;
        }

        return results;
    }

    private static List<RailTransitStationFact> BuildStations(
        IReadOnlyList<StopCandidate> trainStops,
        IReadOnlyList<StopCandidate> subwayStops,
        IReadOnlyList<StationBuildingCandidate> buildingCandidates,
        IReadOnlyDictionary<int, StationBuildingCandidate> buildingByEntityIndex,
        IReadOnlyDictionary<int, NameSystemEntryFact> nameByEntityIndex,
        IReadOnlyDictionary<int, int> ownerEntityIndexByEntityIndex)
    {
        var stations = new List<RailTransitStationFact>();
        var localTrainStopOwnerEntityIndexes = trainStops
            .Where(stop => !stop.IsOutsideConnection && stop.OwnerEntityIndex >= 0)
            .Select(stop => stop.OwnerEntityIndex)
            .ToHashSet();

        foreach (var trainStop in trainStops.Where(stop => stop.HasCustomName && !stop.IsOutsideConnection))
        {
            if (!nameByEntityIndex.TryGetValue(trainStop.StopEntityIndex, out var nameEntry))
            {
                continue;
            }

            stations.Add(
                new RailTransitStationFact(
                    "train",
                    "station",
                    nameEntry.Value,
                    trainStop.StopEntityIndex,
                    nameEntry.StringOffset,
                    trainStop.OwnerEntityIndex,
                    trainStop.StopEntityIndex,
                    nameEntry.Value,
                    new ReadOnlyCollection<int>(
                        trainStop.OwnerEntityIndex >= 0
                            ? [trainStop.OwnerEntityIndex]
                            : []),
                    new ReadOnlyCollection<RailTransitServedLineFact>([])));
        }

        foreach (var building in buildingCandidates.Where(candidate => candidate.HasCustomName))
        {
            if (!nameByEntityIndex.TryGetValue(building.EntityIndex, out var nameEntry))
            {
                continue;
            }

            var ancestry = BuildAncestry(building, buildingByEntityIndex);
            var baseStation = ancestry[^1];
            var role = baseStation.EntityIndex == building.EntityIndex ? "station" : "entrance";
            var baseStationName = nameByEntityIndex.TryGetValue(baseStation.EntityIndex, out var baseStationNameEntry)
                ? baseStationNameEntry.Value
                : baseStation.EntityIndex == building.EntityIndex
                    ? nameEntry.Value
                    : null;
            var ancestryEntityIndexes = ancestry.Select(candidate => candidate.EntityIndex).ToHashSet();
            var matchedTrainStopOwnerEntityIndexes = trainStops
                .Where(stop => !stop.IsOutsideConnection && ancestryEntityIndexes.Contains(stop.OwnerEntityIndex))
                .Select(stop => stop.OwnerEntityIndex)
                .Distinct()
                .OrderBy(entityIndex => entityIndex)
                .ToArray();

            if (matchedTrainStopOwnerEntityIndexes.Length > 0 ||
                localTrainStopOwnerEntityIndexes.Contains(baseStation.EntityIndex) ||
                localTrainStopOwnerEntityIndexes.Contains(building.EntityIndex))
            {
                stations.Add(
                    new RailTransitStationFact(
                        "train",
                        role,
                        nameEntry.Value,
                        building.EntityIndex,
                        nameEntry.StringOffset,
                        building.OwnerEntityIndex,
                        baseStation.EntityIndex,
                        baseStationName,
                        new ReadOnlyCollection<int>(matchedTrainStopOwnerEntityIndexes),
                        new ReadOnlyCollection<RailTransitServedLineFact>([])));
                continue;
            }

            var matchedSubwayStops = subwayStops
                .Where(
                    stop =>
                    {
                        if (ancestryEntityIndexes.Contains(stop.AttachedEntityIndex))
                        {
                            return true;
                        }

                        var stopOwnerChain = BuildEntityOwnerChain(stop.OwnerEntityIndex, ownerEntityIndexByEntityIndex);
                        return stopOwnerChain.Any(ancestryEntityIndexes.Contains);
                    })
                .ToList();
            var inferSubwayFromBuildingTags = matchedSubwayStops.Count == 0 &&
                                              (baseStation.HasSubNet || ancestry.Any(candidate => candidate.HasSubLane));
            if (matchedSubwayStops.Count == 0 && !inferSubwayFromBuildingTags)
            {
                continue;
            }

            var matchedSubwayStopOwnerEntityIndexes = matchedSubwayStops
                .Select(stop => stop.OwnerEntityIndex)
                .Where(entityIndex => entityIndex >= 0)
                .Distinct()
                .OrderBy(entityIndex => entityIndex)
                .ToArray();
            stations.Add(
                new RailTransitStationFact(
                    "subway",
                    role,
                    nameEntry.Value,
                    building.EntityIndex,
                    nameEntry.StringOffset,
                    building.OwnerEntityIndex,
                    baseStation.EntityIndex,
                    baseStationName,
                    new ReadOnlyCollection<int>(matchedSubwayStopOwnerEntityIndexes),
                    new ReadOnlyCollection<RailTransitServedLineFact>([])));
        }

        return stations
            .OrderBy(station => station.Mode, StringComparer.Ordinal)
            .ThenBy(station => station.BaseStationName ?? station.Name, StringComparer.Ordinal)
            .ThenBy(station => station.Role, StringComparer.Ordinal)
            .ThenBy(station => station.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static TransportLineFact ApplyModeAudit(
        TransportLineFact line,
        TransportLineModeAuditLineFact? modeAudit)
    {
        if (modeAudit is null)
        {
            return line with
            {
                ModeEvidenceNotes = new ReadOnlyCollection<string>([])
            };
        }

        return line with
        {
            Mode = ResolveMode(modeAudit),
            IsCargo = modeAudit.IsCargo,
            ModeEvidenceNotes = BuildModeEvidenceNotes(modeAudit)
        };
    }

    private static string ResolveMode(TransportLineModeAuditLineFact modeAudit)
    {
        return string.Equals(modeAudit.CandidateMode, "unresolved", StringComparison.Ordinal)
            ? "unresolved"
            : modeAudit.CandidateMode;
    }

    private static ReadOnlyCollection<string> BuildModeEvidenceNotes(TransportLineModeAuditLineFact modeAudit)
    {
        var notes = new List<string>();

        if (!string.Equals(modeAudit.CandidateMode, "unresolved", StringComparison.Ordinal))
        {
            notes.Add($"stop_family:{modeAudit.CandidateMode}");
        }

        if (!string.IsNullOrWhiteSpace(modeAudit.VehicleModeClue))
        {
            notes.Add($"vehicle_family:{modeAudit.VehicleModeClue}");
        }

        if (modeAudit.HasCargoOwnerClue)
        {
            notes.Add("cargo:owner_clue");
        }

        if (modeAudit.IsCargo == true &&
            !modeAudit.HasCargoOwnerClue)
        {
            notes.Add("cargo:vehicle_prefab");
        }

        if (modeAudit.HasOutsideConnectionClue)
        {
            notes.Add("outside_connection_clue");
        }

        return new ReadOnlyCollection<string>(notes.Distinct(StringComparer.Ordinal).ToList());
    }

    private static List<StationBuildingCandidate> BuildAncestry(
        StationBuildingCandidate building,
        IReadOnlyDictionary<int, StationBuildingCandidate> buildingByEntityIndex)
    {
        var results = new List<StationBuildingCandidate> { building };
        var seen = new HashSet<int> { building.EntityIndex };
        var current = building;

        while (current.OwnerEntityIndex >= 0 &&
               buildingByEntityIndex.TryGetValue(current.OwnerEntityIndex, out var ownerCandidate) &&
               seen.Add(ownerCandidate.EntityIndex))
        {
            results.Add(ownerCandidate);
            current = ownerCandidate;
        }

        return results;
    }

    private static List<int> BuildEntityOwnerChain(
        int entityIndex,
        IReadOnlyDictionary<int, int> ownerEntityIndexByEntityIndex)
    {
        var results = new List<int>();
        var seen = new HashSet<int>();
        var current = entityIndex;

        while (current >= 0 && seen.Add(current))
        {
            results.Add(current);
            if (!ownerEntityIndexByEntityIndex.TryGetValue(current, out current))
            {
                break;
            }
        }

        return results;
    }

    private sealed record TransportLineExtractionResult(
        List<TransportLineFact> TransportLines);

    private sealed record StopCandidate(
        string Mode,
        int StopEntityIndex,
        int StopArchetypeIndex,
        int StopEntityOrdinal,
        int OwnerEntityIndex,
        int AttachedEntityIndex,
        bool HasCustomName,
        bool IsOutsideConnection);

    private sealed record StationBuildingCandidate(
        int EntityIndex,
        int ArchetypeIndex,
        int EntityOrdinal,
        int OwnerEntityIndex,
        bool HasSubNet,
        bool HasSubLane,
        bool HasCustomName);

    private static (int NameEntityIndex, int BaseStationEntityIndex, string Role) CreateStationAuditKey(RailTransitStationFact station)
    {
        return (station.NameEntityIndex, station.BaseStationEntityIndex, station.Role);
    }

    private static (int NameEntityIndex, int BaseStationEntityIndex, string Role) CreateStationAuditKey(TransportStationServiceAuditStationFact station)
    {
        return (station.NameEntityIndex, station.BaseStationEntityIndex, station.Role);
    }

    private static (int NameEntityIndex, int BaseStationEntityIndex, string Role) CreateStationJoinKey(RailTransitStationFact station)
    {
        return (station.NameEntityIndex, station.BaseStationEntityIndex, station.Role);
    }

    private static (int NameEntityIndex, int BaseStationEntityIndex, string Role) CreateStationJoinKey(TransportServiceJoinStationFact station)
    {
        return (station.NameEntityIndex, station.BaseStationEntityIndex, station.Role);
    }

    private static ReadOnlyCollection<RailTransitServedLineFact> ConvertAuditLines(
        IReadOnlyList<TransportStationServiceAuditLineFact> auditLines)
    {
        return new ReadOnlyCollection<RailTransitServedLineFact>(
            auditLines
                .Select(
                    line => new RailTransitServedLineFact(
                        line.LineEntityIndex,
                        line.RouteNumber,
                        line.ColorHex,
                        line.LineName,
                        line.EvidenceNotes))
                .ToList());
    }

    private static ReadOnlyCollection<RailTransitServedLineFact> ConvertJoinLines(
        IReadOnlyList<TransportServiceJoinLineFact> joinLines)
    {
        return new ReadOnlyCollection<RailTransitServedLineFact>(
            joinLines
                .Select(
                    line => new RailTransitServedLineFact(
                        line.LineEntityIndex,
                        line.RouteNumber,
                        line.ColorHex,
                        line.LineName,
                        line.EvidenceNotes))
                .ToList());
    }

    private static Dictionary<int, HashSet<string>> BuildResolvedFacilityModesByLineEntityIndex(
        TransportDomainFacts transportDomainFacts)
    {
        var results = new Dictionary<int, HashSet<string>>();
        var resolvedFacilities = transportDomainFacts.TransportFacilities?
            .Where(
                facility =>
                    string.Equals(facility.ClassificationStatus, "resolved", StringComparison.Ordinal) &&
                    (string.Equals(facility.Mode, "train", StringComparison.Ordinal) ||
                     string.Equals(facility.Mode, "subway", StringComparison.Ordinal)))
            .ToList() ?? [];

        foreach (var station in transportDomainFacts.RailTransitStations.Where(station => string.Equals(station.ServiceJoinStatus, "resolved", StringComparison.Ordinal)))
        {
            var facilityModes = resolvedFacilities
                .Where(
                    facility =>
                        facility.BaseFacilityEntityIndex == station.BaseStationEntityIndex ||
                        facility.NameEntityIndex == station.NameEntityIndex)
                .Select(facility => facility.Mode)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (facilityModes.Count == 0)
            {
                continue;
            }

            foreach (var servedLine in station.ServedLines)
            {
                if (!results.TryGetValue(servedLine.LineEntityIndex, out var modes))
                {
                    modes = new HashSet<string>(StringComparer.Ordinal);
                    results[servedLine.LineEntityIndex] = modes;
                }

                foreach (var facilityMode in facilityModes)
                {
                    modes.Add(facilityMode);
                }
            }
        }

        return results;
    }

    private static TransportLineFact PromoteLineMode(TransportLineFact line, string mode, string evidenceNote)
    {
        var notes = new List<string>(line.ModeEvidenceNotes ?? new ReadOnlyCollection<string>([]))
        {
            evidenceNote
        };

        return line with
        {
            Mode = mode,
            ModeEvidenceNotes = new ReadOnlyCollection<string>(notes.Distinct(StringComparer.Ordinal).ToList())
        };
    }

    private static bool HasModeEvidence(TransportLineFact line, string expectedNote)
    {
        return line.ModeEvidenceNotes?.Contains(expectedNote, StringComparer.Ordinal) == true;
    }
}

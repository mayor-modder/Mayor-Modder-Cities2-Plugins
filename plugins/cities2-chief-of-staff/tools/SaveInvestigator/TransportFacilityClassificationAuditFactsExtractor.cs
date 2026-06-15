using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class TransportFacilityClassificationAuditFactsExtractor
{
    public static TransportFacilityClassificationAuditFacts Extract(
        BuildingDomainFacts buildingDomainFacts,
        TransportDomainFacts transportDomainFacts,
        EntityGraphFacts entityGraphFacts)
    {
        var buildingDescendantsByRoot = BuildBuildingDescendantsByRoot(buildingDomainFacts.Buildings);
        var buildingFacilities = buildingDomainFacts.Buildings
            .Where(building => building.HasCustomName && !string.IsNullOrWhiteSpace(building.CustomName))
            .Select(
                building =>
                {
                    var baseFacilityEntityIndex = ResolveBaseBuildingEntityIndex(building);
                    var relatedEntityIndexes = buildingDescendantsByRoot.TryGetValue(baseFacilityEntityIndex, out var descendants)
                        ? descendants
                        : [];
                    var (candidateRole, candidateMode) = ClassifyBuilding(building);
                    var evidenceNotes = building.ServiceComponentTypes
                        .Select(typeName => $"service:{typeName}")
                        .Concat(building.BuildingOwnerChainEntityIndexes.Select(entityIndex => $"owner_chain:{entityIndex}"))
                        .Distinct(StringComparer.Ordinal)
                        .ToList();

                    return new TransportFacilityClassificationAuditFacilityFact(
                        building.CustomName!,
                        building.EntityIndex,
                        baseFacilityEntityIndex,
                        candidateRole,
                        candidateMode,
                        "candidate",
                        new ReadOnlyCollection<int>(relatedEntityIndexes),
                        new ReadOnlyCollection<string>(evidenceNotes));
                })
            .ToDictionary(facility => facility.NameEntityIndex);

        var stationFacilities = transportDomainFacts.RailTransitStations
            .Select(
                station =>
                {
                    var relatedEntityIndexes = station.MatchedStopOwnerEntityIndexes
                        .Distinct()
                        .OrderBy(entityIndex => entityIndex)
                        .ToList();
                    var evidenceNotes = new List<string>
                    {
                        $"station_mode:{station.Mode}",
                        $"station_role:{station.Role}",
                        $"base_station:{station.BaseStationEntityIndex}"
                    };
                    evidenceNotes.AddRange(
                        station.MatchedStopOwnerEntityIndexes
                            .Distinct()
                            .OrderBy(entityIndex => entityIndex)
                            .Select(entityIndex => $"matched_stop_owner:{entityIndex}"));
                    if (HasOwnerBacklink(entityGraphFacts, station.OwnerEntityIndex))
                    {
                        evidenceNotes.Add($"owner_backlink:{station.OwnerEntityIndex}");
                    }

                    return new TransportFacilityClassificationAuditFacilityFact(
                        station.Name,
                        station.NameEntityIndex,
                        station.BaseStationEntityIndex,
                        station.Role,
                        station.Mode,
                        "candidate",
                        new ReadOnlyCollection<int>(relatedEntityIndexes),
                        new ReadOnlyCollection<string>(evidenceNotes.Distinct(StringComparer.Ordinal).ToList()));
                })
            .ToDictionary(facility => facility.NameEntityIndex);

        var facilityEntityIndexes = buildingFacilities.Keys
            .Concat(stationFacilities.Keys)
            .Distinct()
            .ToList();
        var facilities = facilityEntityIndexes
            .Select(
                entityIndex =>
                {
                    buildingFacilities.TryGetValue(entityIndex, out var buildingFacility);
                    stationFacilities.TryGetValue(entityIndex, out var stationFacility);
                    return MergeFacilityFacts(buildingFacility, stationFacility);
                })
            .ToList();

        var collisionsByName = facilities
            .GroupBy(facility => facility.Name, StringComparer.Ordinal)
            .Where(group => group.Select(facility => facility.BaseFacilityEntityIndex).Distinct().Count() > 1)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        var enrichedFacilities = facilities
            .Select(
                facility =>
                {
                    if (!collisionsByName.TryGetValue(facility.Name, out _))
                    {
                        return facility;
                    }

                    var evidenceNotes = facility.EvidenceNotes.ToList();
                    evidenceNotes.Add($"name_collision:{facility.Name}");
                    return facility with
                    {
                        EvidenceNotes = new ReadOnlyCollection<string>(evidenceNotes.Distinct(StringComparer.Ordinal).ToList())
                    };
                })
            .OrderBy(facility => facility.CandidateMode, StringComparer.Ordinal)
            .ThenBy(facility => facility.CandidateRole, StringComparer.Ordinal)
            .ThenBy(facility => facility.Name, StringComparer.Ordinal)
            .ThenBy(facility => facility.NameEntityIndex)
            .ToList();

        return new TransportFacilityClassificationAuditFacts(
            new ReadOnlyCollection<TransportFacilityClassificationAuditFacilityFact>(enrichedFacilities));
    }

    private static TransportFacilityClassificationAuditFacilityFact MergeFacilityFacts(
        TransportFacilityClassificationAuditFacilityFact? buildingFacility,
        TransportFacilityClassificationAuditFacilityFact? stationFacility)
    {
        if (buildingFacility is null)
        {
            return ResolveStationOnlyFacility(stationFacility!);
        }

        if (stationFacility is null)
        {
            return buildingFacility;
        }

        if (stationFacility.RelatedEntityIndexes.Count > 0)
        {
            var relatedEntityIndexes = buildingFacility.RelatedEntityIndexes
                .Concat(stationFacility.RelatedEntityIndexes)
                .Distinct()
                .OrderBy(entityIndex => entityIndex)
                .ToList();
            var evidenceNotes = buildingFacility.EvidenceNotes
                .Concat(stationFacility.EvidenceNotes)
                .Append($"building_match:{buildingFacility.BaseFacilityEntityIndex}")
                .Append("resolved_by:matched_stop_owner_path")
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return stationFacility with
            {
                BaseFacilityEntityIndex = buildingFacility.BaseFacilityEntityIndex,
                ClassificationStatus = "resolved",
                RelatedEntityIndexes = new ReadOnlyCollection<int>(relatedEntityIndexes),
                EvidenceNotes = new ReadOnlyCollection<string>(evidenceNotes)
            };
        }

        var unresolvedEvidenceNotes = buildingFacility.EvidenceNotes
            .Append("station_candidate_present_without_matched_stop_owner_path")
            .Append("classification_ceiling:no_matched_stop_owner_path")
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var unresolvedRelatedEntityIndexes = buildingFacility.RelatedEntityIndexes
            .Concat(stationFacility.RelatedEntityIndexes)
            .Distinct()
            .OrderBy(entityIndex => entityIndex)
            .ToList();

        return buildingFacility with
        {
            ClassificationStatus = "unresolved",
            RelatedEntityIndexes = new ReadOnlyCollection<int>(unresolvedRelatedEntityIndexes),
            EvidenceNotes = new ReadOnlyCollection<string>(unresolvedEvidenceNotes)
        };
    }

    private static TransportFacilityClassificationAuditFacilityFact ResolveStationOnlyFacility(
        TransportFacilityClassificationAuditFacilityFact stationFacility)
    {
        if (stationFacility.RelatedEntityIndexes.Count == 0)
        {
            return stationFacility with
            {
                ClassificationStatus = "unresolved",
                EvidenceNotes = new ReadOnlyCollection<string>(
                    stationFacility.EvidenceNotes
                        .Concat(["classification_ceiling:no_matched_stop_owner_path"])
                        .Distinct(StringComparer.Ordinal)
                        .ToList())
            };
        }

        return stationFacility with
        {
            ClassificationStatus = "resolved",
            EvidenceNotes = new ReadOnlyCollection<string>(
                stationFacility.EvidenceNotes
                    .Concat(["resolved_by:matched_stop_owner_path"])
                    .Distinct(StringComparer.Ordinal)
                    .ToList())
        };
    }

    private static Dictionary<int, List<int>> BuildBuildingDescendantsByRoot(
        IReadOnlyCollection<BuildingDomainBuildingFact> buildings)
    {
        var results = new Dictionary<int, List<int>>();
        foreach (var building in buildings)
        {
            var rootEntityIndex = ResolveBaseBuildingEntityIndex(building);
            if (!results.TryGetValue(rootEntityIndex, out var descendants))
            {
                descendants = [];
                results[rootEntityIndex] = descendants;
            }

            if (building.EntityIndex != rootEntityIndex)
            {
                descendants.Add(building.EntityIndex);
            }
        }

        foreach (var descendants in results.Values)
        {
            descendants.Sort();
        }

        return results;
    }

    private static int ResolveBaseBuildingEntityIndex(BuildingDomainBuildingFact building)
    {
        return building.BuildingOwnerChainEntityIndexes.Count > 0
            ? building.BuildingOwnerChainEntityIndexes[^1]
            : building.EntityIndex;
    }

    private static (string CandidateRole, string CandidateMode) ClassifyBuilding(BuildingDomainBuildingFact building)
    {
        if (building.ServiceComponentTypes.Any(typeName => typeName.Contains(".Harbor", StringComparison.Ordinal)))
        {
            return ("hub_building", "watercraft");
        }

        if (building.ServiceComponentTypes.Any(typeName => typeName.Contains(".BusStation", StringComparison.Ordinal)))
        {
            return ("terminal_building", "road_vehicle");
        }

        if (building.ServiceComponentTypes.Any(typeName => typeName.Contains(".TransportStation", StringComparison.Ordinal)))
        {
            return ("station_building", "unresolved");
        }

        return ("named_building", "unresolved");
    }

    private static bool HasOwnerBacklink(EntityGraphFacts entityGraphFacts, int targetEntityIndex)
    {
        if (targetEntityIndex < 0)
        {
            return false;
        }

        return entityGraphFacts.Backlinks.Any(
            backlink => backlink.TargetEntityIndex == targetEntityIndex && backlink.IncomingEdgeIndexes.Count > 0);
    }
}

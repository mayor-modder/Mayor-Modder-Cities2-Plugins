using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class TransportReportFactsExtractor
{
    private static readonly IReadOnlySet<string> TramPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "tram"
    };

    public static TransportReportFacts Extract(
        TransportDomainFacts transportDomainFacts,
        SystemTableFacts systemTableFacts,
        TransportTopologyFacts? transportTopologyFacts = null,
        TransportServiceJoinFacts? transportServiceJoinFacts = null)
    {
        var lineNameByEntityIndex = NameSystemLookup.BuildValueByEntityIndex(systemTableFacts);
        var lineDisplayNameByEntityIndex = transportDomainFacts.TransportLines.ToDictionary(
            line => line.EntityIndex,
            line => ResolveLineDisplayName(line, lineNameByEntityIndex));
        var topologyPlatforms = transportTopologyFacts?.Platforms ?? new ReadOnlyCollection<TransportTopologyPlatformFact>([]);
        var lineGroups = transportDomainFacts.TransportLines
            .Select(
                line =>
                {
                    var displayName = lineDisplayNameByEntityIndex[line.EntityIndex];
                    var reportMode = ResolveReportMode(line, displayName);
                    var evidenceNotes = BuildReportEvidenceNotes(line, reportMode);
                    return new TransportReportLineFact(
                        displayName,
                        line.EntityIndex,
                        line.RouteNumber,
                        line.ColorHex,
                        reportMode,
                        line.IsCargo,
                        evidenceNotes);
                })
            .GroupBy(line => line.Mode, StringComparer.Ordinal)
            .Select(
                group =>
                {
                    var lines = group
                        .OrderBy(line => line.RouteNumber)
                        .ThenBy(line => line.ColorHex, StringComparer.Ordinal)
                        .ThenBy(line => line.LineEntityIndex)
                        .ToList();

                    return new TransportReportLineGroupFact(
                        group.Key,
                        lines.Count,
                        new ReadOnlyCollection<TransportReportLineFact>(lines));
                })
            .OrderBy(group => group.Mode, StringComparer.Ordinal)
            .ToList();

        var stationReports = BuildStationReports(transportDomainFacts, topologyPlatforms);
        var stationGroups = stationReports
            .GroupBy(station => station.Mode, StringComparer.Ordinal)
            .Select(
                group =>
                {
                    var stations = group
                        .OrderBy(station => station.Name, StringComparer.Ordinal)
                        .ToList();
                    return new TransportReportStationGroupFact(
                        group.Key,
                        stations.Count,
                        new ReadOnlyCollection<TransportReportStationFact>(stations));
                })
            .OrderBy(group => group.Mode, StringComparer.Ordinal)
            .ToList();

        var queueHotspots = transportDomainFacts.LineQueues
            .OrderByDescending(line => line.TotalWaitingPassengers)
            .ThenByDescending(line => line.MaxStopQueue)
            .ThenBy(line => line.RouteNumber)
            .ThenBy(line => line.ColorHex, StringComparer.Ordinal)
            .Select(
                line => new TransportReportQueueHotspotFact(
                    lineDisplayNameByEntityIndex.TryGetValue(line.EntityIndex, out var lineDisplayName)
                        ? lineDisplayName
                        : $"Route {line.RouteNumber} {line.ColorHex}",
                    line.EntityIndex,
                    line.RouteNumber,
                    line.ColorHex,
                    line.TotalWaitingPassengers,
                    line.MaxStopQueue))
            .ToList();
        var joinCoverage = (transportServiceJoinFacts?.ModeCoverage ?? new ReadOnlyCollection<TransportJoinModeCoverageFact>([]))
            .Select(
                coverage => new TransportReportJoinCoverageFact(
                    coverage.Mode,
                    coverage.CoverageStatus,
                    BuildJoinCoverageSummary(coverage)))
            .OrderBy(coverage => coverage.Mode, StringComparer.Ordinal)
            .ToList();

        return new TransportReportFacts(
            transportDomainFacts.TransportLines.Count,
            ResolveReportFacilityCount(transportDomainFacts),
            new ReadOnlyCollection<TransportReportLineGroupFact>(lineGroups),
            new ReadOnlyCollection<TransportReportStationGroupFact>(stationGroups),
            new ReadOnlyCollection<TransportReportQueueHotspotFact>(queueHotspots),
            new ReadOnlyCollection<TransportReportJoinCoverageFact>(joinCoverage));
    }

    private static List<TransportReportStationFact> BuildStationReports(
        TransportDomainFacts transportDomainFacts,
        IReadOnlyCollection<TransportTopologyPlatformFact> topologyPlatforms)
    {
        if (transportDomainFacts.TransportFacilities?.Count > 0)
        {
            var transportFacilities = transportDomainFacts.TransportFacilities
                .Where(IsTransportFacingFacility)
                .ToList();
            var railStationsByBase = transportDomainFacts.RailTransitStations
                .GroupBy(station => station.BaseStationEntityIndex)
                .ToDictionary(group => group.Key, group => group.ToList());

            return transportFacilities
                .GroupBy(facility => facility.BaseFacilityEntityIndex)
                .Select(group => BuildFacilityReport(group.ToList(), railStationsByBase, topologyPlatforms))
                .OrderBy(station => station.Mode, StringComparer.Ordinal)
                .ThenBy(station => station.Name, StringComparer.Ordinal)
                .ToList();
        }

        return transportDomainFacts.RailTransitStations
            .GroupBy(station => station.BaseStationEntityIndex)
            .Select(group => BuildRailStationReport(group, topologyPlatforms))
            .OrderBy(station => station.Mode, StringComparer.Ordinal)
            .ThenBy(station => station.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static TransportReportStationFact BuildRailStationReport(
        IGrouping<int, RailTransitStationFact> stationGroup,
        IReadOnlyCollection<TransportTopologyPlatformFact> topologyPlatforms)
    {
        var stationGroupList = stationGroup.ToList();
        var rootStation = stationGroup
            .OrderBy(station => station.Name == station.BaseStationName ? 0 : 1)
            .ThenBy(station => station.Role == "station" ? 0 : 1)
            .ThenBy(station => station.Name, StringComparer.Ordinal)
            .First();
        var servedLineNames = stationGroupList
            .SelectMany(station => station.ServedLines)
            .Select(line => line.LineName)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        var candidateLineNames = stationGroupList
            .SelectMany(station => station.CandidateLines ?? new ReadOnlyCollection<RailTransitServedLineFact>([]))
            .Select(line => line.LineName)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        var platformNames = stationGroupList
            .Where(IsPlatformLike)
            .Select(station => station.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        var entranceNames = stationGroupList
            .Where(station => string.Equals(station.Role, "entrance", StringComparison.Ordinal))
            .Select(station => station.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        var outsideDestinationNames = topologyPlatforms
            .Where(platform => MatchesStationGroup(platform, stationGroupList, rootStation))
            .SelectMany(platform => platform.MatchedOutsideStopNames)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        var serviceJoinStatus = ResolveGroupJoinStatus(stationGroupList);

        return new TransportReportStationFact(
            rootStation.BaseStationName ?? rootStation.Name,
            rootStation.Mode,
            serviceJoinStatus,
            new ReadOnlyCollection<string>(servedLineNames),
            new ReadOnlyCollection<string>(candidateLineNames),
            new ReadOnlyCollection<string>(platformNames),
            new ReadOnlyCollection<string>(entranceNames),
            new ReadOnlyCollection<string>(outsideDestinationNames),
            rootStation.Role,
            "unresolved");
    }

    private static TransportReportStationFact BuildFacilityReport(
        IReadOnlyList<TransportFacilityFact> facilityGroup,
        IReadOnlyDictionary<int, List<RailTransitStationFact>> railStationsByBase,
        IReadOnlyCollection<TransportTopologyPlatformFact> topologyPlatforms)
    {
        var rootFacility = facilityGroup
            .OrderBy(facility => facility.Role is "station" or "station_building" ? 0 : facility.Role == "entrance" ? 2 : 1)
            .ThenBy(facility => facility.Name, StringComparer.Ordinal)
            .First();
        var groupNameEntityIndexes = facilityGroup
            .Select(facility => facility.NameEntityIndex)
            .ToHashSet();
        var relatedRailStations = railStationsByBase.TryGetValue(rootFacility.BaseFacilityEntityIndex, out var baseStations)
            ? baseStations
                .Where(
                    station =>
                        station.BaseStationEntityIndex == rootFacility.BaseFacilityEntityIndex ||
                        groupNameEntityIndexes.Contains(station.NameEntityIndex))
                .ToList()
            : [];
        var servedLineNames = relatedRailStations
            .SelectMany(station => station.ServedLines)
            .Select(line => line.LineName)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        var candidateLineNames = relatedRailStations
            .SelectMany(station => station.CandidateLines ?? new ReadOnlyCollection<RailTransitServedLineFact>([]))
            .Select(line => line.LineName)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        var platformNames = relatedRailStations
            .Where(IsPlatformLike)
            .Select(station => station.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        var entranceNames = facilityGroup
            .Where(facility => string.Equals(facility.Role, "entrance", StringComparison.Ordinal))
            .Select(facility => facility.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        var outsideDestinationNames = topologyPlatforms
            .Where(
                platform =>
                    platform.BaseStationEntityIndex == rootFacility.BaseFacilityEntityIndex ||
                    platform.PlatformEntityIndex == rootFacility.BaseFacilityEntityIndex ||
                    facilityGroup.Any(facility => facility.NameEntityIndex == platform.PlatformEntityIndex) ||
                    relatedRailStations.Any(station => station.NameEntityIndex == platform.PlatformEntityIndex))
            .SelectMany(platform => platform.MatchedOutsideStopNames)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();

        return new TransportReportStationFact(
            rootFacility.Name,
            rootFacility.Mode,
            ResolveFacilityJoinStatus(rootFacility, relatedRailStations),
            new ReadOnlyCollection<string>(servedLineNames),
            new ReadOnlyCollection<string>(candidateLineNames),
            new ReadOnlyCollection<string>(platformNames),
            new ReadOnlyCollection<string>(entranceNames),
            new ReadOnlyCollection<string>(outsideDestinationNames),
            rootFacility.Role,
            rootFacility.ClassificationStatus);
    }

    private static bool MatchesStationGroup(
        TransportTopologyPlatformFact platform,
        IReadOnlyCollection<RailTransitStationFact> stationGroup,
        RailTransitStationFact rootStation)
    {
        if (platform.BaseStationEntityIndex == rootStation.BaseStationEntityIndex ||
            platform.PlatformEntityIndex == rootStation.BaseStationEntityIndex)
        {
            return true;
        }

        return stationGroup.Any(
            station =>
                platform.PlatformEntityIndex == station.NameEntityIndex ||
                string.Equals(platform.PlatformName, station.Name, StringComparison.Ordinal) ||
                (!string.IsNullOrWhiteSpace(platform.BaseStationName) &&
                 (string.Equals(platform.BaseStationName, station.Name, StringComparison.Ordinal) ||
                  string.Equals(platform.BaseStationName, rootStation.Name, StringComparison.Ordinal))));
    }

    private static bool IsPlatformLike(RailTransitStationFact station)
    {
        return station.Name.Contains("Platform", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveFacilityJoinStatus(
        TransportFacilityFact rootFacility,
        IReadOnlyCollection<RailTransitStationFact> relatedRailStations)
    {
        if (relatedRailStations.Count == 0)
        {
            return rootFacility.ClassificationStatus;
        }

        return ResolveGroupJoinStatus(relatedRailStations);
    }

    private static int ResolveReportFacilityCount(TransportDomainFacts transportDomainFacts)
    {
        if (transportDomainFacts.TransportFacilities?.Count > 0)
        {
            return transportDomainFacts.TransportFacilities.Count(IsTransportFacingFacility);
        }

        return transportDomainFacts.RailTransitStations.Count;
    }

    private static bool IsTransportFacingFacility(TransportFacilityFact facility)
    {
        return facility.Role is "station" or "entrance" or "station_building" or "terminal_building" or "hub_building";
    }

    private static string ResolveGroupJoinStatus(IEnumerable<RailTransitStationFact> stationGroup)
    {
        if (stationGroup.Any(station => string.Equals(station.ServiceJoinStatus, "resolved", StringComparison.Ordinal)))
        {
            return "resolved";
        }

        if (stationGroup.Any(station => string.Equals(station.ServiceJoinStatus, "candidate_only", StringComparison.Ordinal)))
        {
            return "candidate_only";
        }

        return "unresolved";
    }

    private static string ResolveLineDisplayName(
        TransportLineFact line,
        IReadOnlyDictionary<int, string> lineNameByEntityIndex)
    {
        return lineNameByEntityIndex.TryGetValue(line.EntityIndex, out var lineName)
            ? lineName
            : $"Route {line.RouteNumber} {line.ColorHex}";
    }

    private static string ResolveReportMode(TransportLineFact line, string displayName)
    {
        if (!string.Equals(line.Mode, "unresolved", StringComparison.Ordinal))
        {
            return line.Mode;
        }

        if (line.IsCargo == true &&
            HasEvidence(line, "vehicle_family:rail_vehicle"))
        {
            return "cargo_train";
        }

        if (HasToken(displayName, "bus") &&
            HasEvidence(line, "vehicle_family:road_vehicle"))
        {
            return "bus";
        }

        if (StartsWithUsefulPrefix(displayName, TramPrefixes) &&
            HasEvidence(line, "vehicle_family:rail_vehicle"))
        {
            return "tram";
        }

        if (HasToken(displayName, "train") &&
            HasEvidence(line, "vehicle_family:rail_vehicle"))
        {
            return "train";
        }

        if (HasToken(displayName, "ferry") &&
            HasEvidence(line, "vehicle_family:watercraft"))
        {
            return "ferry";
        }

        if (HasToken(displayName, "ship") &&
            HasEvidence(line, "vehicle_family:watercraft"))
        {
            return "ship";
        }

        return "unresolved";
    }

    private static ReadOnlyCollection<string> BuildReportEvidenceNotes(TransportLineFact line, string reportMode)
    {
        var notes = new List<string>(line.ModeEvidenceNotes ?? new ReadOnlyCollection<string>([]));
        if (!string.Equals(reportMode, line.Mode, StringComparison.Ordinal))
        {
            notes.Add($"report_mode:{reportMode}");
        }

        return new ReadOnlyCollection<string>(notes);
    }

    private static string BuildJoinCoverageSummary(TransportJoinModeCoverageFact coverage)
    {
        if (coverage.StationGroupCount > 0)
        {
            return coverage.CoverageStatus switch
            {
                "resolved_station_joins" =>
                    $"exact joins resolved for all {coverage.SolvedStationGroupCount} of {coverage.StationGroupCount} named station groups",
                "partial_station_joins" =>
                    $"exact joins resolved for {coverage.SolvedStationGroupCount} of {coverage.StationGroupCount} named station groups",
                "exact_carrier_found_but_station_unresolved" =>
                    $"exact carriers found, but 0 of {coverage.StationGroupCount} named station groups are promoted yet",
                "station_inventory_without_exact_carrier" =>
                    $"named station groups found ({coverage.StationGroupCount}), but no exact join carrier is promoted yet",
                "exact_carrier_proven_for_all_station_groups" =>
                    $"exact carriers proven for all {coverage.SolvedStationGroupCount} of {coverage.StationGroupCount} named station groups",
                "exact_carrier_proven_for_some_station_groups" =>
                    $"exact carriers proven for {coverage.SolvedStationGroupCount} of {coverage.StationGroupCount} named station groups",
                _ =>
                    $"{coverage.StationGroupCount} named station groups checked"
            };
        }

        return coverage.CoverageStatus switch
        {
            "exact_carrier_without_station_inventory" =>
                $"exact carriers found on {coverage.ExactCarrierStopCount} of {coverage.CheckedStopCount} checked line-owned stops, but no named station inventory was found",
            "no_proven_join_path_without_station_inventory" =>
                $"checked {coverage.CheckedStopCount} line-owned stops, but found no exact join path and no named station inventory",
            _ =>
                $"checked {coverage.CheckedStopCount} line-owned stops"
        };
    }

    private static bool HasEvidence(TransportLineFact line, string expectedNote)
    {
        return line.ModeEvidenceNotes?.Contains(expectedNote, StringComparer.Ordinal) == true;
    }

    private static bool StartsWithUsefulPrefix(string value, IReadOnlySet<string> prefixes)
    {
        var firstToken = value
            .Split([' ', '-', '_', '/', '(', ')'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        return firstToken is not null && prefixes.Contains(firstToken);
    }

    private static bool HasToken(string value, string expectedToken)
    {
        return value
            .Split([' ', '-', '_', '/', '(', ')'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(token => string.Equals(token, expectedToken, StringComparison.OrdinalIgnoreCase));
    }
}

using System.Collections.ObjectModel;

namespace SaveInvestigator;

internal static class TransportJoinModeCoverageBuilder
{
    public static List<TransportJoinModeCoverageFact> BuildForAudit(
        TransportDomainFacts transportDomainFacts,
        IReadOnlyDictionary<int, List<ConnectedLineCarrierFact>> exactConnectedCarriersByStopEntityIndex,
        IReadOnlyDictionary<int, TransportLineFact> lineByEntityIndex,
        IReadOnlyList<TransportJoinPathAuditStationFact> stations)
    {
        var stationCountByMode = stations
            .GroupBy(station => station.Mode)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var exactStationCountByMode = stations
            .Where(station => string.Equals(station.JoinStatus, "exact_carrier_proven", StringComparison.Ordinal))
            .GroupBy(station => station.Mode)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        return Build(
            transportDomainFacts,
            exactConnectedCarriersByStopEntityIndex,
            lineByEntityIndex,
            stationCountByMode,
            exactStationCountByMode,
            ResolveAuditCoverageStatus,
            "exact_station_group_count");
    }

    public static List<TransportJoinModeCoverageFact> BuildForServiceJoins(
        TransportDomainFacts transportDomainFacts,
        IReadOnlyDictionary<int, List<ConnectedLineCarrierFact>> exactConnectedCarriersByStopEntityIndex,
        IReadOnlyDictionary<int, TransportLineFact> lineByEntityIndex,
        IReadOnlyList<TransportServiceJoinStationFact> stations)
    {
        var stationCountByMode = stations
            .GroupBy(station => station.Mode)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var resolvedStationCountByMode = stations
            .Where(station => string.Equals(station.JoinStatus, "resolved", StringComparison.Ordinal))
            .GroupBy(station => station.Mode)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        return Build(
            transportDomainFacts,
            exactConnectedCarriersByStopEntityIndex,
            lineByEntityIndex,
            stationCountByMode,
            resolvedStationCountByMode,
            ResolveServiceCoverageStatus,
            "resolved_station_group_count");
    }

    private static List<TransportJoinModeCoverageFact> Build(
        TransportDomainFacts transportDomainFacts,
        IReadOnlyDictionary<int, List<ConnectedLineCarrierFact>> exactConnectedCarriersByStopEntityIndex,
        IReadOnlyDictionary<int, TransportLineFact> lineByEntityIndex,
        IReadOnlyDictionary<string, int> stationCountByMode,
        IReadOnlyDictionary<string, int> solvedStationCountByMode,
        Func<int, int, int, string> resolveCoverageStatus,
        string solvedStationEvidenceLabel)
    {
        var modeCoverage = stationCountByMode
            .Select(
                pair =>
                {
                    var solvedStationCount = solvedStationCountByMode.TryGetValue(pair.Key, out var count)
                        ? count
                        : 0;
                    return new TransportJoinModeCoverageFact(
                        pair.Key,
                        resolveCoverageStatus(pair.Value, solvedStationCount, 0),
                        pair.Value,
                        solvedStationCount,
                        0,
                        0,
                        new ReadOnlyCollection<string>(
                            [
                                "mode_source:station_groups",
                                "checked_stop_count:0",
                                "exact_carrier_stop_count:0",
                                $"station_group_count:{pair.Value}",
                                $"{solvedStationEvidenceLabel}:{solvedStationCount}"
                            ]));
                })
            .ToDictionary(fact => fact.Mode, StringComparer.Ordinal);

        var lineOwnedStops = transportDomainFacts.WaitingPassengers.Stops
            .Where(stop => lineByEntityIndex.ContainsKey(stop.OwnerEntityIndex))
            .Select(
                stop =>
                {
                    var line = lineByEntityIndex[stop.OwnerEntityIndex];
                    return new
                    {
                        Mode = ResolveCoverageMode(line),
                        StopEntityIndex = stop.EntityIndex,
                        OwnerLineEntityIndex = stop.OwnerEntityIndex
                    };
                })
            .Where(stop => !string.IsNullOrWhiteSpace(stop.Mode))
            .GroupBy(stop => stop.Mode, StringComparer.Ordinal);

        foreach (var group in lineOwnedStops)
        {
            var checkedStops = group
                .Select(stop => stop.StopEntityIndex)
                .Distinct()
                .OrderBy(entityIndex => entityIndex)
                .ToList();
            var exactCarrierStopCount = group
                .GroupBy(stop => stop.StopEntityIndex)
                .Count(
                    stopGroup =>
                    {
                        var first = stopGroup.First();
                        return exactConnectedCarriersByStopEntityIndex.TryGetValue(first.StopEntityIndex, out var carriers) &&
                               carriers.Any(carrier => carrier.LineEntityIndex == first.OwnerLineEntityIndex);
                    });
            var stationGroupCount = stationCountByMode.TryGetValue(group.Key, out var count)
                ? count
                : 0;
            var solvedStationCount = solvedStationCountByMode.TryGetValue(group.Key, out var solvedCount)
                ? solvedCount
                : 0;
            modeCoverage[group.Key] = new TransportJoinModeCoverageFact(
                group.Key,
                resolveCoverageStatus(stationGroupCount, solvedStationCount, exactCarrierStopCount),
                stationGroupCount,
                solvedStationCount,
                checkedStops.Count,
                exactCarrierStopCount,
                new ReadOnlyCollection<string>(
                    [
                        "mode_source:line_owned_stops",
                        $"checked_stop_count:{checkedStops.Count}",
                        $"exact_carrier_stop_count:{exactCarrierStopCount}",
                        $"station_group_count:{stationGroupCount}",
                        $"{solvedStationEvidenceLabel}:{solvedStationCount}"
                    ]));
        }

        return modeCoverage.Values
            .OrderBy(fact => fact.Mode, StringComparer.Ordinal)
            .ToList();
    }

    private static string ResolveAuditCoverageStatus(
        int stationGroupCount,
        int exactStationCount,
        int exactCarrierStopCount)
    {
        if (stationGroupCount > 0)
        {
            if (exactStationCount >= stationGroupCount)
            {
                return "exact_carrier_proven_for_all_station_groups";
            }

            if (exactStationCount > 0)
            {
                return "exact_carrier_proven_for_some_station_groups";
            }

            return "station_inventory_without_proven_carrier";
        }

        return exactCarrierStopCount > 0
            ? "exact_carrier_without_station_inventory"
            : "no_proven_join_path_without_station_inventory";
    }

    private static string ResolveServiceCoverageStatus(
        int stationGroupCount,
        int resolvedStationCount,
        int exactCarrierStopCount)
    {
        if (stationGroupCount > 0)
        {
            if (resolvedStationCount >= stationGroupCount)
            {
                return "resolved_station_joins";
            }

            if (resolvedStationCount > 0)
            {
                return "partial_station_joins";
            }

            return exactCarrierStopCount > 0
                ? "exact_carrier_found_but_station_unresolved"
                : "station_inventory_without_exact_carrier";
        }

        return exactCarrierStopCount > 0
            ? "exact_carrier_without_station_inventory"
            : "no_proven_join_path_without_station_inventory";
    }

    private static string ResolveCoverageMode(TransportLineFact line)
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

        if (HasEvidence(line, "vehicle_family:road_vehicle"))
        {
            return "road_vehicle";
        }

        if (HasEvidence(line, "vehicle_family:watercraft"))
        {
            return "watercraft";
        }

        if (HasEvidence(line, "vehicle_family:rail_vehicle"))
        {
            return "rail_vehicle";
        }

        return string.Empty;
    }

    private static bool HasEvidence(TransportLineFact line, string expectedNote)
    {
        return line.ModeEvidenceNotes?.Contains(expectedNote, StringComparer.Ordinal) == true;
    }
}

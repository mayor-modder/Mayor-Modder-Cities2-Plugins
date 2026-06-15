using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class TransportServiceJoinFactsExtractor
{
    public static TransportServiceJoinFacts Extract(
        byte[] payload,
        SavePreludeSummary summary,
        TransportDomainFacts transportDomainFacts,
        SystemTableFacts systemTableFacts)
    {
        var lineByEntityIndex = transportDomainFacts.TransportLines.ToDictionary(line => line.EntityIndex);
        var lineNameByEntityIndex = NameSystemLookup.BuildValueByEntityIndex(systemTableFacts);
        var stopOwnersByOwnerEntityIndex = transportDomainFacts.StopOwners
            .GroupBy(stopOwner => stopOwner.OwnerEntityIndex)
            .ToDictionary(group => group.Key, group => group.ToList());
        var routeReferenceBuffers = TransportRouteReferenceBufferReader.Extract(
            payload,
            summary,
            lineByEntityIndex.Keys.ToHashSet());
        var exactConnectedCarriersByStopEntityIndex = TransportConnectedLineCarrierReader.ExtractByConnectedTargetEntityIndex(
            payload,
            summary,
            lineByEntityIndex.Keys.ToHashSet());
        var routeBufferFacts = routeReferenceBuffers.BufferFacts
            .Select(
                fact =>
                    new TransportServiceJoinBufferFact(
                        fact.ArchetypeIndex,
                        fact.EntityCount,
                        fact.JoinComponentType,
                        fact.TrailingByteCount,
                        fact.ResolvedEntityCount,
                        fact.EvidenceNotes))
            .ToList();

        var stations = transportDomainFacts.RailTransitStations
            .Select(
                station =>
                {
                    var exactLines = ResolveStationLines(
                        station,
                        stopOwnersByOwnerEntityIndex,
                        exactConnectedCarriersByStopEntityIndex,
                        routeReferenceBuffers.ReferencesByEntityAndComponent,
                        lineByEntityIndex,
                        lineNameByEntityIndex);
                    return new TransportServiceJoinStationFact(
                        station.Mode,
                        station.Role,
                        station.Name,
                        station.NameEntityIndex,
                        station.BaseStationEntityIndex,
                        exactLines.Count > 0 ? "resolved" : "unresolved",
                        new ReadOnlyCollection<int>(exactLines.Select(line => line.LineEntityIndex).ToList()),
                        new ReadOnlyCollection<TransportServiceJoinLineFact>(exactLines),
                        new ReadOnlyCollection<string>(
                            [
                                $"station_mode:{station.Mode}",
                                $"exact_line_count:{exactLines.Count}"
                            ]));
                })
            .OrderBy(station => station.Mode, StringComparer.Ordinal)
            .ThenBy(station => station.Name, StringComparer.Ordinal)
            .ThenBy(station => station.Role, StringComparer.Ordinal)
            .ToList();
        var modeCoverage = TransportJoinModeCoverageBuilder.BuildForServiceJoins(
            transportDomainFacts,
            exactConnectedCarriersByStopEntityIndex,
            lineByEntityIndex,
            stations);

        return new TransportServiceJoinFacts(
            new ReadOnlyCollection<TransportServiceJoinStationFact>(stations),
            new ReadOnlyCollection<TransportServiceJoinBufferFact>(routeBufferFacts),
            new ReadOnlyCollection<TransportJoinModeCoverageFact>(modeCoverage));
    }

    private static List<TransportServiceJoinLineFact> ResolveStationLines(
        RailTransitStationFact station,
        IReadOnlyDictionary<int, List<RailTransitStopOwnerFact>> stopOwnersByOwnerEntityIndex,
        IReadOnlyDictionary<int, List<ConnectedLineCarrierFact>> exactConnectedCarriersByStopEntityIndex,
        IReadOnlyDictionary<(int EntityIndex, string JoinComponentType), List<int>> routeRefsByEntityAndComponent,
        IReadOnlyDictionary<int, TransportLineFact> lineByEntityIndex,
        IReadOnlyDictionary<int, string> lineNameByEntityIndex)
    {
        var stationStopOwners = station.MatchedStopOwnerEntityIndexes
            .Where(stopOwnersByOwnerEntityIndex.ContainsKey)
            .SelectMany(ownerEntityIndex => stopOwnersByOwnerEntityIndex[ownerEntityIndex])
            .ToList();
        var exactTargetStopEntityIndexes = ResolveExactTargetStopEntityIndexes(station, stationStopOwners);
        var exactConnectedLines = exactTargetStopEntityIndexes
            .SelectMany(
                stopEntityIndex =>
                {
                    if (!exactConnectedCarriersByStopEntityIndex.TryGetValue(stopEntityIndex, out var carrierMatches))
                    {
                        return [];
                    }

                    return carrierMatches
                        .Where(match => lineByEntityIndex.ContainsKey(match.LineEntityIndex))
                        .Select(
                            match =>
                            {
                                var line = lineByEntityIndex[match.LineEntityIndex];
                                lineNameByEntityIndex.TryGetValue(match.LineEntityIndex, out var lineName);
                                return new TransportServiceJoinLineFact(
                                    match.LineEntityIndex,
                                    line.RouteNumber,
                                    line.ColorHex,
                                    lineName,
                                    "Game.Routes.Connected",
                                    new ReadOnlyCollection<string>(
                                        [
                                            "join_component:Game.Routes.Connected",
                                            "join_path:matched_stop_entity<-connected_line_waypoint_entity",
                                            $"join_entity:{match.SourceEntityIndex}",
                                            $"connected_target_stop:{stopEntityIndex}",
                                            $"owner_line_entity:{match.LineEntityIndex}",
                                            $"source_archetype:{match.SourceArchetypeIndex}"
                                        ]));
                            });
                });

        var candidates = new List<(int EntityIndex, string JoinComponentType, string EvidencePath)>
        {
            (station.NameEntityIndex, TransportRouteReferenceBufferReader.ConnectedRouteTypeName, "station_name_entity"),
            (station.NameEntityIndex, TransportRouteReferenceBufferReader.SubRouteTypeName, "station_name_entity"),
            (station.OwnerEntityIndex, TransportRouteReferenceBufferReader.ConnectedRouteTypeName, "station_owner_entity"),
            (station.OwnerEntityIndex, TransportRouteReferenceBufferReader.SubRouteTypeName, "station_owner_entity"),
            (station.BaseStationEntityIndex, TransportRouteReferenceBufferReader.ConnectedRouteTypeName, "station_base_entity"),
            (station.BaseStationEntityIndex, TransportRouteReferenceBufferReader.SubRouteTypeName, "station_base_entity")
        };

        return exactConnectedLines
            .Concat(
                candidates
                    .Where(candidate => candidate.EntityIndex >= 0)
                    .SelectMany(
                        candidate =>
                        {
                            if (!routeRefsByEntityAndComponent.TryGetValue((candidate.EntityIndex, candidate.JoinComponentType), out var lineEntityIndexes))
                            {
                                return [];
                            }

                            return lineEntityIndexes
                                .Where(lineByEntityIndex.ContainsKey)
                                .Select(
                                    lineEntityIndex =>
                                    {
                                        var line = lineByEntityIndex[lineEntityIndex];
                                        lineNameByEntityIndex.TryGetValue(lineEntityIndex, out var lineName);
                                        return new TransportServiceJoinLineFact(
                                            lineEntityIndex,
                                            line.RouteNumber,
                                            line.ColorHex,
                                            lineName,
                                            candidate.JoinComponentType,
                                            new ReadOnlyCollection<string>(
                                                [
                                                    $"join_component:{candidate.JoinComponentType}",
                                                    $"join_path:{candidate.EvidencePath}",
                                                    $"join_entity:{candidate.EntityIndex}"
                                                ]));
                                    });
                        }))
            .GroupBy(line => line.LineEntityIndex)
            .Select(group => group.First())
            .OrderBy(line => line.RouteNumber)
            .ThenBy(line => line.ColorHex, StringComparer.Ordinal)
            .ThenBy(line => line.LineEntityIndex)
            .ToList();
    }

    private static IReadOnlyList<int> ResolveExactTargetStopEntityIndexes(
        RailTransitStationFact station,
        IReadOnlyList<RailTransitStopOwnerFact> stationStopOwners)
    {
        if (stationStopOwners.Any(stopOwner => stopOwner.StopEntityIndex == station.NameEntityIndex))
        {
            return [station.NameEntityIndex];
        }

        if (stationStopOwners.Any(stopOwner => stopOwner.StopEntityIndex == station.BaseStationEntityIndex))
        {
            return [station.BaseStationEntityIndex];
        }

        return stationStopOwners
            .Select(stopOwner => stopOwner.StopEntityIndex)
            .Distinct()
            .OrderBy(entityIndex => entityIndex)
            .ToList();
    }

}

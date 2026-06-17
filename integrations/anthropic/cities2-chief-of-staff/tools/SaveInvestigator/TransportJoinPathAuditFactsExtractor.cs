using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class TransportJoinPathAuditFactsExtractor
{
    private const string ConnectedTypeName = "Game.Routes.Connected";

    public static TransportJoinPathAuditFacts Extract(
        byte[] payload,
        SavePreludeSummary summary,
        TransportDomainFacts transportDomainFacts)
    {
        var lineByEntityIndex = transportDomainFacts.TransportLines.ToDictionary(line => line.EntityIndex);
        var routeReferenceBuffers = TransportRouteReferenceBufferReader.Extract(
            payload,
            summary,
            lineByEntityIndex.Keys.ToHashSet());
        var exactConnectedCarriersByStopEntityIndex = TransportConnectedLineCarrierReader.ExtractByConnectedTargetEntityIndex(
            payload,
            summary,
            lineByEntityIndex.Keys.ToHashSet());
        var stopOwnersByOwnerEntityIndex = transportDomainFacts.StopOwners
            .GroupBy(stopOwner => stopOwner.OwnerEntityIndex)
            .ToDictionary(group => group.Key, group => group.ToList());

        var stations = transportDomainFacts.RailTransitStations
            .Select(
                station =>
                {
                    var candidateCarriers = ResolveCandidateCarriers(
                        station,
                        stopOwnersByOwnerEntityIndex,
                        exactConnectedCarriersByStopEntityIndex,
                        routeReferenceBuffers.ReferencesByEntityAndComponent,
                        lineByEntityIndex);
                    return new TransportJoinPathAuditStationFact(
                        station.Mode,
                        station.Role,
                        station.Name,
                        station.NameEntityIndex,
                        station.BaseStationEntityIndex,
                        ResolveJoinStatus(candidateCarriers),
                        new ReadOnlyCollection<TransportJoinPathAuditCarrierFact>(candidateCarriers),
                        new ReadOnlyCollection<string>(
                            [
                                $"station_mode:{station.Mode}",
                                $"candidate_carrier_count:{candidateCarriers.Count}"
                            ]));
                })
            .OrderBy(station => station.Mode, StringComparer.Ordinal)
            .ThenBy(station => station.Name, StringComparer.Ordinal)
            .ThenBy(station => station.Role, StringComparer.Ordinal)
            .ToList();

        var routeBuffers = routeReferenceBuffers.BufferFacts
            .Select(
                fact =>
                    new TransportJoinPathAuditBufferFact(
                        fact.ArchetypeIndex,
                        fact.EntityCount,
                        fact.JoinComponentType,
                        fact.TrailingByteCount,
                        fact.ResolvedEntityCount,
                        fact.EvidenceNotes))
            .ToList();
        var modeCoverage = TransportJoinModeCoverageBuilder.BuildForAudit(
            transportDomainFacts,
            exactConnectedCarriersByStopEntityIndex,
            lineByEntityIndex,
            stations);

        return new TransportJoinPathAuditFacts(
            new ReadOnlyCollection<TransportJoinPathAuditStationFact>(stations),
            new ReadOnlyCollection<TransportJoinPathAuditBufferFact>(routeBuffers),
            new ReadOnlyCollection<TransportJoinModeCoverageFact>(modeCoverage));
    }

    private static List<TransportJoinPathAuditCarrierFact> ResolveCandidateCarriers(
        RailTransitStationFact station,
        IReadOnlyDictionary<int, List<RailTransitStopOwnerFact>> stopOwnersByOwnerEntityIndex,
        IReadOnlyDictionary<int, List<ConnectedLineCarrierFact>> exactConnectedCarriersByStopEntityIndex,
        IReadOnlyDictionary<(int EntityIndex, string JoinComponentType), List<int>> routeRefsByEntityAndComponent,
        IReadOnlyDictionary<int, TransportLineFact> lineByEntityIndex)
    {
        var candidates = new List<(int EntityIndex, string EntityRole)>
        {
            (station.NameEntityIndex, "station_name_entity")
        };

        if (station.BaseStationEntityIndex != station.NameEntityIndex)
        {
            candidates.Add((station.BaseStationEntityIndex, "station_base_entity"));
        }

        if (station.OwnerEntityIndex >= 0)
        {
            candidates.Add((station.OwnerEntityIndex, "station_owner_entity"));
        }

        foreach (var matchedStopOwnerEntityIndex in station.MatchedStopOwnerEntityIndexes)
        {
            candidates.Add((matchedStopOwnerEntityIndex, "matched_stop_owner_entity"));
            if (!stopOwnersByOwnerEntityIndex.TryGetValue(matchedStopOwnerEntityIndex, out var stopOwners))
            {
                continue;
            }

            foreach (var stopOwner in stopOwners)
            {
                candidates.Add((stopOwner.StopEntityIndex, "matched_stop_entity"));
                if (stopOwner.AttachedEntityIndex >= 0)
                {
                    candidates.Add((stopOwner.AttachedEntityIndex, "attached_entity"));
                }
            }
        }

        var routeBufferCarriers = candidates
            .Where(candidate => candidate.EntityIndex >= 0)
            .Distinct()
            .SelectMany(
                candidate =>
                    new[] { TransportRouteReferenceBufferReader.SubRouteTypeName, TransportRouteReferenceBufferReader.ConnectedRouteTypeName }
                        .Where(joinComponentType => routeRefsByEntityAndComponent.ContainsKey((candidate.EntityIndex, joinComponentType)))
                        .Select(
                            joinComponentType =>
                            {
                                var candidateLineEntityIndexes = routeRefsByEntityAndComponent[(candidate.EntityIndex, joinComponentType)]
                                    .Where(lineByEntityIndex.ContainsKey)
                                    .Distinct()
                                    .OrderBy(entityIndex => entityIndex)
                                    .ToList();
                                return new TransportJoinPathAuditCarrierFact(
                                    candidate.EntityIndex,
                                    candidate.EntityRole,
                                    "candidate",
                                    joinComponentType,
                                    new ReadOnlyCollection<int>(candidateLineEntityIndexes),
                                    new ReadOnlyCollection<string>(
                                        [
                                            $"join_path:{candidate.EntityRole}",
                                            $"join_component:{joinComponentType}",
                                            $"candidate_line_count:{candidateLineEntityIndexes.Count}"
                                        ]));
                            }))
            .OrderBy(carrier => carrier.JoinEntityIndex)
            .ThenBy(carrier => carrier.JoinEntityRole, StringComparer.Ordinal)
            .ThenBy(carrier => carrier.JoinComponentType, StringComparer.Ordinal)
            .ToList();

        var exactConnectedCarriers = candidates
            .Where(candidate => string.Equals(candidate.EntityRole, "matched_stop_entity", StringComparison.Ordinal))
            .SelectMany(
                candidate =>
                {
                    if (!exactConnectedCarriersByStopEntityIndex.TryGetValue(candidate.EntityIndex, out var carrierMatches))
                    {
                        return [];
                    }

                    return carrierMatches
                        .Where(match => lineByEntityIndex.ContainsKey(match.LineEntityIndex))
                        .Select(
                            match =>
                                new TransportJoinPathAuditCarrierFact(
                                    match.SourceEntityIndex,
                                    "connected_line_waypoint_entity",
                                    "exact",
                                    ConnectedTypeName,
                                    new ReadOnlyCollection<int>([match.LineEntityIndex]),
                                    new ReadOnlyCollection<string>(
                                        [
                                            "join_path:matched_stop_entity<-connected_line_waypoint_entity",
                                            $"connected_target_stop:{candidate.EntityIndex}",
                                            $"owner_line_entity:{match.LineEntityIndex}",
                                            $"source_archetype:{match.SourceArchetypeIndex}"
                                        ])));
                })
            .OrderBy(carrier => carrier.JoinEntityIndex)
            .ThenBy(carrier => carrier.JoinComponentType, StringComparer.Ordinal)
            .ToList();

        return routeBufferCarriers
            .Concat(exactConnectedCarriers)
            .GroupBy(carrier => (carrier.JoinEntityIndex, carrier.JoinEntityRole, carrier.JoinComponentType))
            .Select(group => group.First())
            .OrderBy(carrier => carrier.JoinEntityIndex)
            .ThenBy(carrier => carrier.JoinEntityRole, StringComparer.Ordinal)
            .ThenBy(carrier => carrier.JoinComponentType, StringComparer.Ordinal)
            .ToList();
    }

    private static string ResolveJoinStatus(IReadOnlyList<TransportJoinPathAuditCarrierFact> candidateCarriers)
    {
        if (candidateCarriers.Any(carrier => string.Equals(carrier.CarrierKind, "exact", StringComparison.Ordinal)))
        {
            return "exact_carrier_proven";
        }

        return candidateCarriers.Count > 0 ? "candidate_only" : "unresolved";
    }

}

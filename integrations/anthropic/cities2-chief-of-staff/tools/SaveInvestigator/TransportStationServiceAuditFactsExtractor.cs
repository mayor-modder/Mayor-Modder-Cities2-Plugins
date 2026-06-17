using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class TransportStationServiceAuditFactsExtractor
{
    public static TransportStationServiceAuditFacts Extract(
        TransportDomainFacts transportDomainFacts,
        SystemTableFacts systemTableFacts,
        EntityGraphFacts entityGraphFacts)
    {
        var lineNameByEntityIndex = NameSystemLookup.BuildValueByEntityIndex(systemTableFacts);
        var incomingSourceEntityIndexesByTarget = BuildIncomingSourceEntityIndexesByTarget(entityGraphFacts);

        var stations = transportDomainFacts.RailTransitStations
            .Select(
                station =>
                {
                    var resolvedCandidates = BuildResolvedCandidates(station, transportDomainFacts, lineNameByEntityIndex);
                    var candidateLines = resolvedCandidates.Count > 0
                        ? resolvedCandidates
                        : BuildHeuristicCandidates(station, transportDomainFacts, lineNameByEntityIndex);
                    var candidateLineEntityIndexes = candidateLines
                        .Select(line => line.LineEntityIndex)
                        .Distinct()
                        .OrderBy(entityIndex => entityIndex)
                        .ToList();
                    var stationIncomingEntityIndexes = incomingSourceEntityIndexesByTarget.TryGetValue(
                        station.BaseStationEntityIndex,
                        out var incomingEntityIndexes)
                        ? incomingEntityIndexes
                        : [];
                    var joinStatus = resolvedCandidates.Count > 0
                        ? "resolved"
                        : candidateLines.Count > 0
                            ? "candidate_only"
                            : "unresolved";
                    var evidenceNotes = BuildStationEvidenceNotes(station, joinStatus, candidateLines.Count);

                    return new TransportStationServiceAuditStationFact(
                        station.Mode,
                        station.Role,
                        station.Name,
                        station.NameEntityIndex,
                        station.BaseStationEntityIndex,
                        joinStatus,
                        station.MatchedStopOwnerEntityIndexes,
                        new ReadOnlyCollection<int>(stationIncomingEntityIndexes),
                        new ReadOnlyCollection<int>(candidateLineEntityIndexes),
                        new ReadOnlyCollection<TransportStationServiceAuditLineFact>(candidateLines),
                        new ReadOnlyCollection<string>(evidenceNotes));
                })
            .OrderBy(station => station.Mode, StringComparer.Ordinal)
            .ThenBy(station => station.Name, StringComparer.Ordinal)
            .ThenBy(station => station.Role, StringComparer.Ordinal)
            .ToList();

        return new TransportStationServiceAuditFacts(new ReadOnlyCollection<TransportStationServiceAuditStationFact>(stations));
    }

    private static List<TransportStationServiceAuditLineFact> BuildResolvedCandidates(
        RailTransitStationFact station,
        TransportDomainFacts transportDomainFacts,
        IReadOnlyDictionary<int, string> lineNameByEntityIndex)
    {
        var linesByEntityIndex = transportDomainFacts.TransportLines.ToDictionary(line => line.EntityIndex);
        return station.ServedLines
            .Select(
                servedLine =>
                {
                    linesByEntityIndex.TryGetValue(servedLine.LineEntityIndex, out var lineFact);
                    var lineName = servedLine.LineName ??
                                   (lineNameByEntityIndex.TryGetValue(servedLine.LineEntityIndex, out var matchedName) ? matchedName : null);
                    return new TransportStationServiceAuditLineFact(
                        servedLine.LineEntityIndex,
                        servedLine.RouteNumber,
                        servedLine.ColorHex,
                        lineName,
                        lineFact?.Mode ?? "unresolved",
                        lineFact?.IsCargo,
                        "resolved_station_join",
                        servedLine.EvidenceComponentTypes);
                })
            .OrderBy(line => line.RouteNumber)
            .ThenBy(line => line.ColorHex, StringComparer.Ordinal)
            .ThenBy(line => line.LineEntityIndex)
            .ToList();
    }

    private static List<TransportStationServiceAuditLineFact> BuildHeuristicCandidates(
        RailTransitStationFact station,
        TransportDomainFacts transportDomainFacts,
        IReadOnlyDictionary<int, string> lineNameByEntityIndex)
    {
        var exactModeMatches = transportDomainFacts.TransportLines
            .Where(line => string.Equals(line.Mode, station.Mode, StringComparison.Ordinal) && !ShouldExcludeForStation(station, line))
            .Select(
                line => new TransportStationServiceAuditLineFact(
                    line.EntityIndex,
                    line.RouteNumber,
                    line.ColorHex,
                    lineNameByEntityIndex.TryGetValue(line.EntityIndex, out var lineName) ? lineName : null,
                    line.Mode,
                    line.IsCargo,
                    "exact_mode_match",
                    new ReadOnlyCollection<string>(
                        [
                            $"station_mode:{station.Mode}",
                            $"line_mode:{line.Mode}"
                        ])))
            .OrderBy(line => line.RouteNumber)
            .ThenBy(line => line.ColorHex, StringComparer.Ordinal)
            .ThenBy(line => line.LineEntityIndex)
            .ToList();
        if (exactModeMatches.Count > 0)
        {
            return exactModeMatches;
        }

        if (!string.Equals(station.Mode, "train", StringComparison.Ordinal))
        {
            return [];
        }

        var stationTokens = Tokenize(station.Name);
        return transportDomainFacts.TransportLines
            .Where(
                line => string.Equals(line.Mode, "unresolved", StringComparison.Ordinal) &&
                        !ShouldExcludeForStation(station, line) &&
                        line.ModeEvidenceNotes?.Contains("vehicle_family:rail_vehicle", StringComparer.Ordinal) == true &&
                        lineNameByEntityIndex.TryGetValue(line.EntityIndex, out var lineName) &&
                        TryResolveSharedToken(stationTokens, lineName, out _))
            .Select(
                line => new TransportStationServiceAuditLineFact(
                    line.EntityIndex,
                    line.RouteNumber,
                    line.ColorHex,
                    lineNameByEntityIndex.TryGetValue(line.EntityIndex, out var lineName) ? lineName : null,
                    line.Mode,
                    line.IsCargo,
                    "name_overlap_inference",
                    new ReadOnlyCollection<string>(
                        [
                            $"station_mode:{station.Mode}",
                            $"line_vehicle_family:rail_vehicle",
                            $"shared_name_token:{ResolveSharedTokenValue(stationTokens, lineNameByEntityIndex.TryGetValue(line.EntityIndex, out var matchedLineName) ? matchedLineName : null)}"
                        ])))
            .OrderBy(line => line.RouteNumber)
            .ThenBy(line => line.ColorHex, StringComparer.Ordinal)
            .ThenBy(line => line.LineEntityIndex)
            .ToList();
    }

    private static bool ShouldExcludeForStation(RailTransitStationFact station, TransportLineFact line)
    {
        return string.Equals(station.Mode, "train", StringComparison.Ordinal) && line.IsCargo == true;
    }

    private static HashSet<string> Tokenize(string value)
    {
        return value
            .Split([' ', '-', '_', '/', '(', ')'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.Trim().ToLowerInvariant())
            .Where(IsUsefulToken)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool TryResolveSharedToken(HashSet<string> stationTokens, string lineName, out string sharedToken)
    {
        foreach (var token in Tokenize(lineName))
        {
            if (stationTokens.Contains(token))
            {
                sharedToken = token;
                return true;
            }
        }

        sharedToken = string.Empty;
        return false;
    }

    private static string ResolveSharedTokenValue(HashSet<string> stationTokens, string? lineName)
    {
        return lineName is not null && TryResolveSharedToken(stationTokens, lineName, out var sharedToken)
            ? sharedToken
            : "unknown";
    }

    private static bool IsUsefulToken(string token)
    {
        return token.Length >= 3 &&
               token is not ("station" or "platform" or "line" or "train" or "tram" or "subway" or "ferry" or "ship" or "cargo" or "inner" or "outer");
    }

    private static Dictionary<int, List<int>> BuildIncomingSourceEntityIndexesByTarget(EntityGraphFacts entityGraphFacts)
    {
        var edgesByIndex = entityGraphFacts.Edges
            .Select((edge, edgeIndex) => (edge, edgeIndex))
            .ToDictionary(pair => pair.edgeIndex, pair => pair.edge);
        var result = new Dictionary<int, List<int>>();

        foreach (var backlink in entityGraphFacts.Backlinks)
        {
            var sourceEntityIndexes = backlink.IncomingEdgeIndexes
                .Where(edgesByIndex.ContainsKey)
                .Select(edgeIndex => edgesByIndex[edgeIndex].SourceEntityIndex)
                .Distinct()
                .OrderBy(entityIndex => entityIndex)
                .ToList();
            result[backlink.TargetEntityIndex] = sourceEntityIndexes;
        }

        return result;
    }

    private static List<string> BuildStationEvidenceNotes(
        RailTransitStationFact station,
        string joinStatus,
        int candidateLineCount)
    {
        var notes = new List<string>
        {
            $"station_mode:{station.Mode}",
            $"join_status:{joinStatus}",
            $"candidate_line_count:{candidateLineCount}",
            $"matched_stop_owner_count:{station.MatchedStopOwnerEntityIndexes.Count}"
        };

        if (station.BaseStationEntityIndex != station.NameEntityIndex)
        {
            notes.Add("name_role:child_entity");
        }

        return notes;
    }
}

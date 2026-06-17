using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class RemainingRailIdentityFactsExtractor
{
    public static RemainingRailIdentityFacts Extract(
        TransportDomainFacts transportDomainFacts,
        TransportLineModeAuditFacts transportLineModeAuditFacts,
        TransportStationServiceAuditFacts transportStationServiceAuditFacts,
        SystemTableFacts systemTableFacts)
    {
        var lineNameByEntityIndex = NameSystemLookup.BuildValueByEntityIndex(systemTableFacts);
        var unresolvedLineEntityIndexes = transportDomainFacts.TransportLines
            .Where(line => string.Equals(line.Mode, "unresolved", StringComparison.Ordinal))
            .Select(line => line.EntityIndex)
            .ToHashSet();

        var tramComparisonPrefabs = transportLineModeAuditFacts.Lines
            .Where(
                line => string.Equals(line.VehicleModeClue, "rail_vehicle", StringComparison.Ordinal) &&
                        line.IsCargo != true &&
                        line.VehiclePrefabEntityIndex is not null &&
                        lineNameByEntityIndex.TryGetValue(line.LineEntityIndex, out var lineName) &&
                        lineName.StartsWith("Tram ", StringComparison.OrdinalIgnoreCase))
            .Select(line => BuildPrefabComparisonKey(line.VehiclePrefabEntityIndex, line.VehiclePrefabComponentTypes))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();

        var trainCandidateLineEntityIndexes = transportStationServiceAuditFacts.Stations
            .Where(station => string.Equals(station.Mode, "train", StringComparison.Ordinal))
            .SelectMany(station => station.CandidateLines)
            .Select(line => line.LineEntityIndex)
            .Distinct()
            .OrderBy(entityIndex => entityIndex)
            .ToList();
        var trainCandidateLineEntityIndexSet = trainCandidateLineEntityIndexes.ToHashSet();

        var subwayStationNames = transportStationServiceAuditFacts.Stations
            .Where(station => string.Equals(station.Mode, "subway", StringComparison.Ordinal))
            .Select(station => station.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var unresolvedLines = transportLineModeAuditFacts.Lines
            .Where(
                line => string.Equals(line.CandidateMode, "unresolved", StringComparison.Ordinal) &&
                        string.Equals(line.VehicleModeClue, "rail_vehicle", StringComparison.Ordinal) &&
                        unresolvedLineEntityIndexes.Contains(line.LineEntityIndex) &&
                        line.IsCargo != true)
            .Select(
                line =>
                {
                    var displayName = lineNameByEntityIndex.TryGetValue(line.LineEntityIndex, out var lineName)
                        ? lineName
                        : $"Route {line.RouteNumber} {line.ColorHex}";
                    var stopFamilies = line.StopFamilyClues
                        .Select(clue => clue.StopFamily)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToList();
                    var stationJoins = transportStationServiceAuditFacts.Stations
                        .SelectMany(
                            station => station.CandidateLines
                                .Where(candidate => candidate.LineEntityIndex == line.LineEntityIndex)
                                .Select(
                                    candidate => new RemainingRailIdentityStationJoinFact(
                                        station.Name,
                                        station.Mode,
                                        station.JoinStatus,
                                        candidate.MatchKind,
                                        candidate.EvidenceNotes)))
                        .OrderBy(join => join.StationMode, StringComparer.Ordinal)
                        .ThenBy(join => join.StationName, StringComparer.Ordinal)
                        .ToList();

                    var exclusionNotes = BuildExclusionNotes(
                        line,
                        stopFamilies,
                        stationJoins,
                        tramComparisonPrefabs,
                        trainCandidateLineEntityIndexSet,
                        subwayStationNames);
                    var candidateModes = BuildCandidateModes(exclusionNotes);

                    return new RemainingRailIdentityLineFact(
                        displayName,
                        line.LineEntityIndex,
                        line.RouteNumber,
                        line.ColorHex,
                        line.VehicleModeClue ?? "unknown",
                        line.VehiclePrefabEntityIndex,
                        line.VehiclePrefabComponentTypes,
                        new ReadOnlyCollection<string>(stopFamilies),
                        new ReadOnlyCollection<RemainingRailIdentityStationJoinFact>(stationJoins),
                        new ReadOnlyCollection<string>(exclusionNotes),
                        new ReadOnlyCollection<string>(candidateModes));
                })
            .OrderBy(line => line.RouteNumber)
            .ThenBy(line => line.ColorHex, StringComparer.Ordinal)
            .ThenBy(line => line.LineEntityIndex)
            .ToList();

        return new RemainingRailIdentityFacts(
            unresolvedLines.Count,
            new ReadOnlyCollection<string>(tramComparisonPrefabs),
            new ReadOnlyCollection<int>(trainCandidateLineEntityIndexes),
            new ReadOnlyCollection<string>(subwayStationNames),
            new ReadOnlyCollection<RemainingRailIdentityLineFact>(unresolvedLines));
    }

    private static List<string> BuildExclusionNotes(
        TransportLineModeAuditLineFact line,
        IReadOnlyCollection<string> stopFamilies,
        IReadOnlyCollection<RemainingRailIdentityStationJoinFact> stationJoins,
        IReadOnlyCollection<string> tramComparisonPrefabs,
        IReadOnlySet<int> trainCandidateLineEntityIndexes,
        IReadOnlyCollection<string> subwayStationNames)
    {
        var notes = new List<string>();

        if (line.IsCargo != true)
        {
            notes.Add("exclude:cargo_train:not_cargo");
        }

        var prefabKey = BuildPrefabComparisonKey(line.VehiclePrefabEntityIndex, line.VehiclePrefabComponentTypes);
        if (tramComparisonPrefabs.Contains(prefabKey, StringComparer.Ordinal))
        {
            notes.Add("candidate:tram:matches_named_tram_prefab_cluster");
        }
        else
        {
            notes.Add("exclude:tram:vehicle_prefab_distinct_from_named_tram_cluster");
        }

        if (trainCandidateLineEntityIndexes.Contains(line.LineEntityIndex) || stationJoins.Any(join => string.Equals(join.StationMode, "train", StringComparison.Ordinal)))
        {
            notes.Add("candidate:train:referenced_by_train_station_audit");
        }
        else
        {
            notes.Add("exclude:train:not_referenced_by_train_station_audit");
        }

        if (stopFamilies.Count == 0)
        {
            notes.Add("evidence:no_stop_family_clues");
        }

        if (subwayStationNames.Count > 0)
        {
            notes.Add($"candidate:subway:unresolved_subway_station_count={subwayStationNames.Count}");
        }

        return notes;
    }

    private static List<string> BuildCandidateModes(IReadOnlyCollection<string> exclusionNotes)
    {
        var excludedModes = new HashSet<string>(
            exclusionNotes
                .Where(note => note.StartsWith("exclude:", StringComparison.Ordinal))
                .Select(
                    note =>
                    {
                        var parts = note.Split(':', 3);
                        return parts.Length >= 2 ? parts[1] : string.Empty;
                    })
                .Where(value => value.Length > 0),
            StringComparer.Ordinal);

        var candidates = new List<string>();
        foreach (var mode in new[] { "cargo_train", "tram", "train", "subway" })
        {
            if (!excludedModes.Contains(mode))
            {
                candidates.Add(mode);
            }
        }

        return candidates;
    }

    private static string BuildPrefabComparisonKey(int? vehiclePrefabEntityIndex, IReadOnlyCollection<string> vehiclePrefabComponentTypes)
    {
        var componentSignature = string.Join("|", vehiclePrefabComponentTypes.OrderBy(value => value, StringComparer.Ordinal));
        return $"{vehiclePrefabEntityIndex?.ToString() ?? "null"}::{componentSignature}";
    }
}

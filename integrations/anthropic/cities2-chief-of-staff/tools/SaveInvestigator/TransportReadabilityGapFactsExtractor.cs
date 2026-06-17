using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class TransportReadabilityGapFactsExtractor
{
    private static readonly IReadOnlyDictionary<string, int> SeverityRankByValue =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["high"] = 3,
            ["medium"] = 2,
            ["low"] = 1
        };

    public static TransportReadabilityGapFacts Extract(
        TransportDomainFacts transportDomainFacts,
        TransportReportFacts transportReportFacts,
        SaveCoverageFacts saveCoverageFacts)
    {
        var gaps = new List<TransportReadabilityGapFact>();
        var transportFacilityGroups = TransportFacilityGrouping.BuildTransportFacingGroups(transportDomainFacts);
        var lineDisplayNameByEntityIndex = transportReportFacts.LineGroups
            .SelectMany(group => group.Lines)
            .ToDictionary(line => line.LineEntityIndex, line => line.DisplayName);

        var unresolvedLines = transportDomainFacts.TransportLines
            .Where(line => string.Equals(line.Mode, "unresolved", StringComparison.Ordinal))
            .OrderBy(line => line.RouteNumber)
            .ThenBy(line => line.ColorHex, StringComparer.Ordinal)
            .ThenBy(line => line.EntityIndex)
            .ToList();
        if (unresolvedLines.Count > 0)
        {
            gaps.Add(
                new TransportReadabilityGapFact(
                    "identity_gap",
                    "high",
                    $"{unresolvedLines.Count} lines still lack exact save-proven mode labels.",
                    unresolvedLines.Count,
                    new ReadOnlyCollection<string>(
                        unresolvedLines
                            .Select(line => FormatLineGapExample(line, lineDisplayNameByEntityIndex))
                            .Take(5)
                            .ToList())));
        }

        var classificationGaps = transportFacilityGroups
            .Where(group => !string.Equals(group.ClassificationStatus, "resolved", StringComparison.Ordinal))
            .OrderBy(group => group.Mode, StringComparer.Ordinal)
            .ThenBy(group => group.Name, StringComparer.Ordinal)
            .ToList();
        if (classificationGaps.Count > 0)
        {
            gaps.Add(
                new TransportReadabilityGapFact(
                    "classification_gap",
                    "high",
                    $"{classificationGaps.Count} named transport facilities still lack proved role/mode classification.",
                    classificationGaps.Count,
                    new ReadOnlyCollection<string>(
                        classificationGaps
                            .Select(FormatFacilityGapExample)
                            .Take(5)
                            .ToList())));
        }

        var stationJoinGaps = ResolveStationJoinGapExamples(transportDomainFacts, transportFacilityGroups);
        if (stationJoinGaps.Count > 0)
        {
            gaps.Add(
                new TransportReadabilityGapFact(
                    "join_gap",
                    "high",
                    $"{stationJoinGaps.Count} named service-bearing facilities still lack proved served-line joins.",
                    stationJoinGaps.Count,
                    new ReadOnlyCollection<string>(stationJoinGaps.Take(5).ToList())));
        }

        foreach (var openItem in saveCoverageFacts.OpenItems.Where(item => string.Equals(item.Scope, "transport", StringComparison.Ordinal)))
        {
            var category = openItem.Description.Contains("runtime-synthesized", StringComparison.OrdinalIgnoreCase)
                ? "runtime_synthesized_gap"
                : "unresolved_save_layout_gap";
            var severity = category == "runtime_synthesized_gap" ? "medium" : "high";
            gaps.Add(
                new TransportReadabilityGapFact(
                    category,
                    severity,
                    openItem.Description,
                    1,
                    new ReadOnlyCollection<string>([])));
        }

        return new TransportReadabilityGapFacts(
            new ReadOnlyCollection<TransportReadabilityGapFact>(
                gaps
                    .OrderByDescending(gap => ResolveSeverityRank(gap.Severity))
                    .ThenBy(gap => gap.Category, StringComparer.Ordinal)
                    .ToList()));
    }

    private static int ResolveSeverityRank(string severity)
    {
        return SeverityRankByValue.TryGetValue(severity, out var rank)
            ? rank
            : 0;
    }

    private static string FormatLineGapExample(
        TransportLineFact line,
        IReadOnlyDictionary<int, string> lineDisplayNameByEntityIndex)
    {
        var displayName = lineDisplayNameByEntityIndex.TryGetValue(line.EntityIndex, out var value)
            ? value
            : $"route {line.RouteNumber} {line.ColorHex}";
        return $"{displayName} [line {line.EntityIndex}]";
    }

    private static string FormatStationGapExample(RailTransitStationFact station)
    {
        return $"{station.Mode}:{station.Role}:{station.Name} [name {station.NameEntityIndex}, base {station.BaseStationEntityIndex}]";
    }

    private static List<string> ResolveStationJoinGapExamples(
        TransportDomainFacts transportDomainFacts,
        IReadOnlyList<TransportFacilityGroupFact> transportFacilityGroups)
    {
        if (transportFacilityGroups.Count > 0)
        {
            return transportFacilityGroups
                .Where(group => string.Equals(group.ClassificationStatus, "resolved", StringComparison.Ordinal))
                .Where(group => group.RelatedRailStations.Count > 0)
                .Where(group => !string.Equals(TransportFacilityGrouping.ResolveServiceJoinStatus(group), "resolved", StringComparison.Ordinal))
                .OrderBy(group => group.Mode, StringComparer.Ordinal)
                .ThenBy(group => group.Name, StringComparer.Ordinal)
                .Select(FormatFacilityGapExample)
                .ToList();
        }

        return transportDomainFacts.RailTransitStations
            .Where(station => !string.Equals(station.ServiceJoinStatus, "resolved", StringComparison.Ordinal))
            .GroupBy(station => station.NameEntityIndex)
            .Select(group => group.First())
            .OrderBy(station => station.Mode, StringComparer.Ordinal)
            .ThenBy(station => station.Name, StringComparer.Ordinal)
            .Select(FormatStationGapExample)
            .ToList();
    }

    private static string FormatFacilityGapExample(TransportFacilityGroupFact facilityGroup)
    {
        return $"{facilityGroup.Mode}:{facilityGroup.Role}:{facilityGroup.Name} [name {facilityGroup.NameEntityIndex}, base {facilityGroup.BaseFacilityEntityIndex}]";
    }
}

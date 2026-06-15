using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class TransitUnderstandingFactsExtractor
{
    private const int LineIdentityWeight = 35;
    private const int StationInventoryWeight = 20;
    private const int ServedLineJoinWeight = 20;
    private const int TopologyWeight = 15;
    private const int OperationalMetricsWeight = 10;

    public static TransitUnderstandingFacts Extract(
        TransportDomainFacts transportDomainFacts,
        TransportReportFacts transportReportFacts,
        TransportTopologyFacts transportTopologyFacts)
    {
        var transportFacilityGroups = TransportFacilityGrouping.BuildTransportFacingGroups(transportDomainFacts);
        var categories = new List<TransitUnderstandingCategoryFact>
        {
            BuildLineIdentityCategory(transportDomainFacts),
            BuildStationInventoryCategory(transportReportFacts, transportFacilityGroups),
            BuildServedLineJoinCategory(transportDomainFacts, transportFacilityGroups),
            BuildTopologyCategory(transportTopologyFacts),
            BuildOperationalMetricsCategory(transportDomainFacts)
        };

        var earnedWeight = categories.Sum(category => category.EarnedWeight);
        var remainingBlockers = categories
            .SelectMany(category => category.RemainingBlockers)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(blocker => blocker, StringComparer.Ordinal)
            .ToList();

        return new TransitUnderstandingFacts(
            earnedWeight,
            new ReadOnlyCollection<TransitUnderstandingCategoryFact>(categories),
            new ReadOnlyCollection<string>(remainingBlockers));
    }

    private static TransitUnderstandingCategoryFact BuildLineIdentityCategory(TransportDomainFacts transportDomainFacts)
    {
        var lines = transportDomainFacts.TransportLines
            .ToList();
        var resolvedCount = lines.Count(
            line => !string.Equals(line.Mode, "unresolved", StringComparison.Ordinal));
        var unresolvedLines = lines
            .Where(line => string.Equals(line.Mode, "unresolved", StringComparison.Ordinal))
            .Select(FormatLineBlocker)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return BuildCategory(
            "line_identity",
            resolvedCount,
            lines.Count,
            LineIdentityWeight,
            $"Resolved mode identity for {resolvedCount} of {lines.Count} transport lines from the save-backed domain layer.",
            unresolvedLines);
    }

    private static TransitUnderstandingCategoryFact BuildStationInventoryCategory(
        TransportReportFacts transportReportFacts,
        IReadOnlyList<TransportFacilityGroupFact> transportFacilityGroups)
    {
        if (transportFacilityGroups.Count > 0)
        {
            var resolvedCount = transportFacilityGroups.Count(group => string.Equals(group.ClassificationStatus, "resolved", StringComparison.Ordinal));
            var blockers = transportFacilityGroups
                .Where(group => !string.Equals(group.ClassificationStatus, "resolved", StringComparison.Ordinal))
                .Select(FormatFacilityBlocker)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();

            return BuildCategory(
                "station_inventory",
                resolvedCount,
                transportFacilityGroups.Count,
                StationInventoryWeight,
                $"Transport-facing facility classification is resolved for {resolvedCount} of {transportFacilityGroups.Count} named facility groups.",
                blockers);
        }

        var stations = transportReportFacts.StationGroups
            .SelectMany(group => group.Stations)
            .ToList();
        var namedCount = stations.Count(station => !string.IsNullOrWhiteSpace(station.Name));

        return BuildCategory(
            "station_inventory",
            namedCount,
            stations.Count,
            StationInventoryWeight,
            $"Readable station, entrance, and platform names are available for {namedCount} of {stations.Count} reported station groups.",
            []);
    }

    private static TransitUnderstandingCategoryFact BuildServedLineJoinCategory(
        TransportDomainFacts transportDomainFacts,
        IReadOnlyList<TransportFacilityGroupFact> transportFacilityGroups)
    {
        if (transportFacilityGroups.Count > 0)
        {
            var serviceBearingGroups = transportFacilityGroups
                .Where(group => string.Equals(group.ClassificationStatus, "resolved", StringComparison.Ordinal))
                .Where(group => group.RelatedRailStations.Count > 0)
                .ToList();
            var facilityResolvedCount = serviceBearingGroups.Count(
                group => string.Equals(TransportFacilityGrouping.ResolveServiceJoinStatus(group), "resolved", StringComparison.Ordinal));
            var facilityCandidateCount = serviceBearingGroups.Count(
                group => string.Equals(TransportFacilityGrouping.ResolveServiceJoinStatus(group), "candidate_only", StringComparison.Ordinal));
            var unresolvedGroups = serviceBearingGroups
                .Where(group => !string.Equals(TransportFacilityGrouping.ResolveServiceJoinStatus(group), "resolved", StringComparison.Ordinal))
                .Select(FormatFacilityBlocker)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            var facilityEarnedWeight = ScoreResolvedCandidateUnresolved(facilityResolvedCount, facilityCandidateCount, serviceBearingGroups.Count, ServedLineJoinWeight);
            var facilityStatus = ResolveStatus(facilityEarnedWeight, ServedLineJoinWeight);

            return new TransitUnderstandingCategoryFact(
                "served_line_joins",
                facilityStatus,
                facilityResolvedCount,
                serviceBearingGroups.Count,
                ServedLineJoinWeight,
                facilityEarnedWeight,
                $"Exact served-line joins are resolved for {facilityResolvedCount} of {serviceBearingGroups.Count} service-bearing facility groups, with {facilityCandidateCount} more still only candidate-backed.",
                new ReadOnlyCollection<string>(unresolvedGroups));
        }

        var stations = transportDomainFacts.RailTransitStations
            .ToList();
        var resolvedCount = stations.Count(station => string.Equals(station.ServiceJoinStatus, "resolved", StringComparison.Ordinal));
        var candidateCount = stations.Count(station => string.Equals(station.ServiceJoinStatus, "candidate_only", StringComparison.Ordinal));
        var unresolvedStations = stations
            .Where(station => !string.Equals(station.ServiceJoinStatus, "resolved", StringComparison.Ordinal))
            .Select(FormatStationBlocker)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        var earnedWeight = ScoreResolvedCandidateUnresolved(resolvedCount, candidateCount, stations.Count, ServedLineJoinWeight);
        var status = ResolveStatus(earnedWeight, ServedLineJoinWeight);

        return new TransitUnderstandingCategoryFact(
            "served_line_joins",
            status,
            resolvedCount,
            stations.Count,
            ServedLineJoinWeight,
            earnedWeight,
            $"Exact served-line joins are resolved for {resolvedCount} of {stations.Count} named station entities, with {candidateCount} more still only candidate-backed.",
            new ReadOnlyCollection<string>(unresolvedStations));
    }

    private static TransitUnderstandingCategoryFact BuildTopologyCategory(TransportTopologyFacts transportTopologyFacts)
    {
        var platforms = transportTopologyFacts.Platforms.ToList();
        var fullyExplainedCount = platforms.Count(platform => string.Equals(platform.TopologyStatus, "outside_named_platform", StringComparison.Ordinal));
        var partialCount = platforms.Count(
            platform =>
                string.Equals(platform.TopologyStatus, "outside_named_platform_no_node_proof", StringComparison.Ordinal) ||
                string.Equals(platform.TopologyStatus, "connected_platform_unmatched_name", StringComparison.Ordinal));
        var unresolvedPlatforms = platforms
            .Where(
                platform =>
                    !string.Equals(platform.TopologyStatus, "outside_named_platform", StringComparison.Ordinal) &&
                    !string.Equals(platform.TopologyStatus, "outside_named_platform_no_node_proof", StringComparison.Ordinal))
            .Select(platform => platform.PlatformName)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        var earnedWeight = ScoreResolvedCandidateUnresolved(fullyExplainedCount, partialCount, platforms.Count, TopologyWeight);
        var status = ResolveStatus(earnedWeight, TopologyWeight);

        return new TransitUnderstandingCategoryFact(
            "topology",
            status,
            fullyExplainedCount,
            platforms.Count,
            TopologyWeight,
            earnedWeight,
            $"Outside-facing topology is fully matched for {fullyExplainedCount} platforms, with {partialCount} more only partially explained.",
            new ReadOnlyCollection<string>(unresolvedPlatforms));
    }

    private static TransitUnderstandingCategoryFact BuildOperationalMetricsCategory(TransportDomainFacts transportDomainFacts)
    {
        var totalChecks = 3;
        var solvedCount = 0;

        if (transportDomainFacts.TransportLines.Count > 0 &&
            transportDomainFacts.TransportLines.All(line => line.VehicleInterval >= 0))
        {
            solvedCount++;
        }

        if (transportDomainFacts.LineQueues.Count > 0)
        {
            solvedCount++;
        }

        if (transportDomainFacts.WaitingPassengers.Stops.Count > 0)
        {
            solvedCount++;
        }

        return BuildCategory(
            "operational_metrics",
            solvedCount,
            totalChecks,
            OperationalMetricsWeight,
            $"Saved line intervals, queue totals, and stop waiting counts are all available for this transport snapshot.",
            []);
    }

    private static TransitUnderstandingCategoryFact BuildCategory(
        string category,
        int solvedCount,
        int totalCount,
        int weight,
        string summary,
        IReadOnlyList<string> blockers)
    {
        var earnedWeight = ScoreExactOnly(solvedCount, totalCount, weight);
        var status = ResolveStatus(earnedWeight, weight);

        return new TransitUnderstandingCategoryFact(
            category,
            status,
            solvedCount,
            totalCount,
            weight,
            earnedWeight,
            summary,
            new ReadOnlyCollection<string>(blockers.ToList()));
    }

    private static int ScoreExactOnly(int solvedCount, int totalCount, int weight)
    {
        if (totalCount <= 0)
        {
            return weight;
        }

        return (int)Math.Round(weight * (solvedCount / (double)totalCount), MidpointRounding.AwayFromZero);
    }

    private static int ScoreResolvedCandidateUnresolved(int resolvedCount, int candidateCount, int totalCount, int weight)
    {
        if (totalCount <= 0)
        {
            return weight;
        }

        var progress = (resolvedCount + (candidateCount * 0.5)) / totalCount;
        return (int)Math.Round(weight * progress, MidpointRounding.AwayFromZero);
    }

    private static string ResolveStatus(int earnedWeight, int weight)
    {
        if (earnedWeight >= weight)
        {
            return "full";
        }

        return earnedWeight > 0
            ? "partial"
            : "unresolved";
    }

    private static string FormatLineBlocker(TransportLineFact line)
    {
        return $"line {line.EntityIndex} route {line.RouteNumber} {line.ColorHex}";
    }

    private static string FormatStationBlocker(RailTransitStationFact station)
    {
        return $"{station.Mode}:{station.Role}:{station.Name} [name {station.NameEntityIndex}, base {station.BaseStationEntityIndex}]";
    }

    private static string FormatFacilityBlocker(TransportFacilityGroupFact facilityGroup)
    {
        return $"{facilityGroup.Mode}:{facilityGroup.Role}:{facilityGroup.Name} [name {facilityGroup.NameEntityIndex}, base {facilityGroup.BaseFacilityEntityIndex}]";
    }
}

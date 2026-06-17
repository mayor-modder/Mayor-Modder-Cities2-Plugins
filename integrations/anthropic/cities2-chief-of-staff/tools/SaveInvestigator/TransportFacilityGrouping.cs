using System.Collections.ObjectModel;

namespace SaveInvestigator;

internal sealed record TransportFacilityGroupFact(
    string Name,
    int NameEntityIndex,
    int BaseFacilityEntityIndex,
    string Role,
    string Mode,
    string ClassificationStatus,
    ReadOnlyCollection<TransportFacilityFact> Facilities,
    ReadOnlyCollection<RailTransitStationFact> RelatedRailStations);

internal static class TransportFacilityGrouping
{
    public static IReadOnlyList<TransportFacilityGroupFact> BuildTransportFacingGroups(TransportDomainFacts transportDomainFacts)
    {
        var transportFacilitiesSource = transportDomainFacts.TransportFacilities;
        if (transportFacilitiesSource is null || transportFacilitiesSource.Count <= 0)
        {
            return [];
        }

        var transportFacilities = transportFacilitiesSource
            .Where(IsTransportFacingFacility)
            .ToList();
        var railStationsByBase = transportDomainFacts.RailTransitStations
            .GroupBy(station => station.BaseStationEntityIndex)
            .ToDictionary(group => group.Key, group => group.ToList());

        return transportFacilities
            .GroupBy(facility => facility.BaseFacilityEntityIndex)
            .Select(
                group =>
                {
                    var facilityGroup = group.ToList();
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

                    return new TransportFacilityGroupFact(
                        rootFacility.Name,
                        rootFacility.NameEntityIndex,
                        rootFacility.BaseFacilityEntityIndex,
                        rootFacility.Role,
                        rootFacility.Mode,
                        rootFacility.ClassificationStatus,
                        new ReadOnlyCollection<TransportFacilityFact>(facilityGroup),
                        new ReadOnlyCollection<RailTransitStationFact>(relatedRailStations));
                })
            .OrderBy(group => group.Mode, StringComparer.Ordinal)
            .ThenBy(group => group.Name, StringComparer.Ordinal)
            .ThenBy(group => group.NameEntityIndex)
            .ToList();
    }

    public static string ResolveServiceJoinStatus(TransportFacilityGroupFact facilityGroup)
    {
        if (facilityGroup.RelatedRailStations.Any(station => string.Equals(station.ServiceJoinStatus, "resolved", StringComparison.Ordinal)))
        {
            return "resolved";
        }

        if (facilityGroup.RelatedRailStations.Any(station => string.Equals(station.ServiceJoinStatus, "candidate_only", StringComparison.Ordinal)))
        {
            return "candidate_only";
        }

        return "unresolved";
    }

    private static bool IsTransportFacingFacility(TransportFacilityFact facility)
    {
        return facility.Role is "station" or "entrance" or "station_building" or "terminal_building" or "hub_building";
    }
}

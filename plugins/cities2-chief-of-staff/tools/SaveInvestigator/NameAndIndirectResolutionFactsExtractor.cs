using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class NameAndIndirectResolutionFactsExtractor
{
    public static NameAndIndirectResolutionFacts Extract(
        SystemTableFacts systemTableFacts,
        TargetedSaveFacts targetedFacts,
        RailTransitStationFacts railTransitStationFacts,
        PassengerTrainStationFacts passengerTrainStationFacts)
    {
        var nameByEntityIndex = NameSystemLookup.BuildEntryByEntityIndex(systemTableFacts);

        var lineNames = targetedFacts.TransportLines
            .Where(line => nameByEntityIndex.ContainsKey(line.EntityIndex))
            .Select(
                line =>
                {
                    var nameEntry = nameByEntityIndex[line.EntityIndex];
                    return new ResolvedLineNameFact(
                        line.EntityIndex,
                        line.RouteNumber,
                        line.ColorHex,
                        nameEntry.Value,
                        nameEntry.EntityIndex,
                        nameEntry.StringOffset);
                })
            .OrderBy(line => line.RouteNumber)
            .ThenBy(line => line.ColorHex, StringComparer.Ordinal)
            .ToList();

        var railTransitNames = railTransitStationFacts.Stations
            .Where(station => nameByEntityIndex.ContainsKey(station.NameEntityIndex))
            .Select(
                station =>
                {
                    var nameEntry = nameByEntityIndex[station.NameEntityIndex];
                    return new ResolvedEntityNameFact(
                        station.Mode,
                        station.Role,
                        station.NameEntityIndex,
                        nameEntry.Value,
                        nameEntry.EntityIndex,
                        nameEntry.StringOffset);
                })
            .OrderBy(item => item.Mode, StringComparer.Ordinal)
            .ThenBy(item => item.Role, StringComparer.Ordinal)
            .ThenBy(item => item.Value, StringComparer.Ordinal)
            .ToList();

        var passengerTrainNames = passengerTrainStationFacts.Stations
            .Where(station => nameByEntityIndex.ContainsKey(station.NameEntityIndex))
            .Select(
                station =>
                {
                    var nameEntry = nameByEntityIndex[station.NameEntityIndex];
                    return new ResolvedEntityNameFact(
                        "train",
                        "station_or_platform",
                        station.NameEntityIndex,
                        nameEntry.Value,
                        nameEntry.EntityIndex,
                        nameEntry.StringOffset);
                })
            .OrderBy(item => item.Value, StringComparer.Ordinal)
            .ThenBy(item => item.TargetEntityIndex)
            .ToList();

        return new NameAndIndirectResolutionFacts(
            new ReadOnlyCollection<ResolvedLineNameFact>(lineNames),
            new ReadOnlyCollection<ResolvedEntityNameFact>(railTransitNames),
            new ReadOnlyCollection<ResolvedEntityNameFact>(passengerTrainNames));
    }
}

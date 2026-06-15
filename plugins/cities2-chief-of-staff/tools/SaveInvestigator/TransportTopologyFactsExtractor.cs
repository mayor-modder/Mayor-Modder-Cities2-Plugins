using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class TransportTopologyFactsExtractor
{
    public static TransportTopologyFacts Extract(
        TransportDomainFacts transportDomainFacts,
        SystemTableFacts systemTableFacts,
        RailTrackConnectivityFacts railTrackConnectivityFacts)
    {
        var outsideTrainStops = transportDomainFacts.StopOwners
            .Where(
                stop => string.Equals(stop.Mode, "train", StringComparison.Ordinal) &&
                        stop.IsOutsideConnection)
            .Select(
                stop =>
                {
                    var name = systemTableFacts.NameSystem?.Entries.FirstOrDefault(entry => entry.EntityIndex == stop.StopEntityIndex)?.Value;
                    return string.IsNullOrWhiteSpace(name)
                        ? null
                        : new TransportTopologyOutsideTrainStopFact(
                            name,
                            stop.StopEntityIndex,
                            stop.OwnerEntityIndex,
                            stop.AttachedEntityIndex);
                })
            .Where(stop => stop is not null)
            .Cast<TransportTopologyOutsideTrainStopFact>()
            .GroupBy(stop => stop.StopEntityIndex)
            .Select(group => group.First())
            .OrderBy(stop => stop.Name, StringComparer.Ordinal)
            .ThenBy(stop => stop.StopEntityIndex)
            .ToList();
        var outsideStopNameMap = outsideTrainStops.ToDictionary(stop => stop.Name, StringComparer.OrdinalIgnoreCase);
        var edgesByIndex = railTrackConnectivityFacts.Edges.ToDictionary(edge => edge.EdgeEntityIndex);

        var platforms = transportDomainFacts.RailTransitStations
            .Where(
                station => string.Equals(station.Mode, "train", StringComparison.Ordinal) &&
                           station.Name.Contains("Platform", StringComparison.Ordinal))
            .Select(
                station =>
                {
                    var stationEdgeEntityIndexes = transportDomainFacts.StopOwners
                        .Where(
                            stop => string.Equals(stop.Mode, "train", StringComparison.Ordinal) &&
                                    (stop.StopEntityIndex == station.NameEntityIndex ||
                                     station.MatchedStopOwnerEntityIndexes.Contains(stop.OwnerEntityIndex)) &&
                                    stop.AttachedEntityIndex >= 0)
                        .Select(stop => stop.AttachedEntityIndex)
                        .Distinct()
                        .OrderBy(entityIndex => entityIndex)
                        .ToList();
                    var connectedNodeEntityIndexes = stationEdgeEntityIndexes
                        .Where(edgesByIndex.ContainsKey)
                        .SelectMany(edgeEntityIndex => edgesByIndex[edgeEntityIndex].ConnectedNodes)
                        .Select(node => node.NodeEntityIndex)
                        .Distinct()
                        .OrderBy(entityIndex => entityIndex)
                        .ToList();
                    var destinationToken = ExtractDestinationToken(station.Name, station.BaseStationName);
                    var matchedOutsideStopNames = string.IsNullOrWhiteSpace(destinationToken)
                        ? []
                        : outsideTrainStops
                            .Where(stop => string.Equals(stop.Name, destinationToken, StringComparison.OrdinalIgnoreCase))
                            .Select(stop => stop.Name)
                            .Distinct(StringComparer.Ordinal)
                            .OrderBy(name => name, StringComparer.Ordinal)
                            .ToList();
                    var topologyStatus = matchedOutsideStopNames.Count > 0 && connectedNodeEntityIndexes.Count > 0
                        ? "outside_named_platform"
                        : matchedOutsideStopNames.Count > 0
                            ? "outside_named_platform_no_node_proof"
                            : connectedNodeEntityIndexes.Count > 0
                                ? "connected_platform_unmatched_name"
                                : "local_or_unresolved_platform";
                    var evidenceNotes = new List<string>
                    {
                        $"platform_destination_token:{destinationToken.ToLowerInvariant()}",
                        $"attached_edge_count:{stationEdgeEntityIndexes.Count}",
                        $"connected_node_count:{connectedNodeEntityIndexes.Count}"
                    };
                    if (matchedOutsideStopNames.Count > 0)
                    {
                        evidenceNotes.Add("outside_stop_name_match");
                    }

                    return new TransportTopologyPlatformFact(
                        station.Name,
                        station.BaseStationName ?? station.Name,
                        station.NameEntityIndex,
                        station.BaseStationEntityIndex,
                        topologyStatus,
                        new ReadOnlyCollection<int>(stationEdgeEntityIndexes),
                        new ReadOnlyCollection<int>(connectedNodeEntityIndexes),
                        new ReadOnlyCollection<string>(matchedOutsideStopNames),
                        new ReadOnlyCollection<string>(evidenceNotes));
                })
            .OrderBy(platform => platform.BaseStationName, StringComparer.Ordinal)
            .ThenBy(platform => platform.PlatformName, StringComparer.Ordinal)
            .ToList();

        return new TransportTopologyFacts(
            new ReadOnlyCollection<TransportTopologyOutsideTrainStopFact>(outsideTrainStops),
            new ReadOnlyCollection<TransportTopologyPlatformFact>(platforms));
    }

    private static string ExtractDestinationToken(string platformName, string? baseStationName)
    {
        var normalized = platformName.Trim();
        if (normalized.EndsWith(" Platform", StringComparison.Ordinal))
        {
            normalized = normalized[..^" Platform".Length];
        }

        var trimmedBaseStationName = baseStationName?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedBaseStationName) &&
            !string.Equals(trimmedBaseStationName, platformName.Trim(), StringComparison.Ordinal))
        {
            var prefix = trimmedBaseStationName + " - ";
            if (normalized.StartsWith(prefix, StringComparison.Ordinal))
            {
                normalized = normalized[prefix.Length..];
            }
        }
        else
        {
            var separatorIndex = normalized.IndexOf(" - ", StringComparison.Ordinal);
            if (separatorIndex >= 0)
            {
                normalized = normalized[(separatorIndex + " - ".Length)..];
            }
        }

        return normalized.Trim();
    }
}

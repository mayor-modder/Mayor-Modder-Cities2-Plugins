using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class ExternalConnectionsFactsExtractor
{
    public static ExternalConnectionsFacts Extract(
        TransportDomainFacts transportDomainFacts,
        EconomyDomainFacts economyDomainFacts)
    {
        var totalOutsideConnections = Math.Max(
            transportDomainFacts.OutsideConnections.Count,
            economyDomainFacts.Summary.OutsideConnections);
        var cargoTransportLines = transportDomainFacts.TransportLines.Count(line => line.IsCargo == true);
        var transportOutsideConnections = transportDomainFacts.OutsideConnections.Count;
        var economyOutsideConnections = economyDomainFacts.Summary.OutsideConnections;

        return new ExternalConnectionsFacts(
            totalOutsideConnections,
            cargoTransportLines,
            transportOutsideConnections,
            economyOutsideConnections,
            "unresolved",
            totalOutsideConnections > 0 ? "partial" : "insufficient",
            $"External connection coverage currently proves {totalOutsideConnections} outside connections and {cargoTransportLines} cargo transport lines, but not import/export flow semantics.",
            new ReadOnlyCollection<string>(
                [
                    $"coverage:outside_connections={totalOutsideConnections}",
                    $"coverage:transport_outside_connections={transportOutsideConnections}",
                    $"coverage:economy_outside_connections={economyOutsideConnections}",
                    $"coverage:cargo_transport_lines={cargoTransportLines}"
                ]),
            new ReadOnlyCollection<string>(
                [
                    "import/export flow semantics and freight bottleneck attribution remain unresolved"
                ]));
    }
}

namespace SaveInvestigator;

public static class RailTransitStationFactsExtractor
{
    [Obsolete("Compatibility shim. Prefer extracting TransportDomainFacts once and then calling the overload that accepts TransportDomainFacts.")]
    public static RailTransitStationFacts Extract(
        byte[] payload,
        SavePreludeSummary summary,
        TargetedSaveFacts targetedFacts,
        SystemBufferFacts systemBufferFacts)
    {
        _ = targetedFacts;
        _ = systemBufferFacts;

        var systemBufferCatalogFacts = SystemBufferCatalogFactsExtractor.Extract(payload, summary);
        var systemTableFacts = SystemTableFactsExtractor.Extract(payload, summary, systemBufferCatalogFacts);
        var transportDomainFacts = TransportDomainFactsExtractor.Extract(
            payload,
            summary,
            systemTableFacts,
            TransportDomainFactsExtractor.BuildCompatibilityEntityGraph(payload, summary));
        return Extract(transportDomainFacts);
    }

    public static RailTransitStationFacts Extract(TransportDomainFacts transportDomainFacts)
    {
        return new RailTransitStationFacts(
            transportDomainFacts.StopOwners,
            transportDomainFacts.RailTransitStations);
    }

    public static RailTransitStationFacts Extract(
        TransportDomainFacts transportDomainFacts,
        TransportStationServiceAuditFacts transportStationServiceAuditFacts)
    {
        return Extract(TransportDomainFactsExtractor.ApplyStationServiceAudit(
            transportDomainFacts,
            transportStationServiceAuditFacts));
    }

    public static RailTransitStationFacts Extract(
        TransportDomainFacts transportDomainFacts,
        TransportServiceJoinFacts transportServiceJoinFacts)
    {
        return Extract(TransportDomainFactsExtractor.ApplyTransportServiceJoinFacts(
            transportDomainFacts,
            transportServiceJoinFacts));
    }
}

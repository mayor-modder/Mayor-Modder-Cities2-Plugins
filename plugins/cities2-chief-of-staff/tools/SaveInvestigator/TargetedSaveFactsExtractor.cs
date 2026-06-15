namespace SaveInvestigator;

public static class TargetedSaveFactsExtractor
{
    [Obsolete("Compatibility shim. Prefer extracting TransportDomainFacts once and then calling the overload that accepts TransportDomainFacts.")]
    public static TargetedSaveFacts Extract(byte[] payload, SavePreludeSummary summary)
    {
        var transportDomainFacts = TransportDomainFactsExtractor.Extract(
            payload,
            summary,
            new SystemTableFacts(
                new System.Collections.ObjectModel.ReadOnlyCollection<SystemTableReviewFact>([]),
                null),
            TransportDomainFactsExtractor.BuildCompatibilityEntityGraph(payload, summary));
        return Extract(transportDomainFacts);
    }

    public static TargetedSaveFacts Extract(TransportDomainFacts transportDomainFacts)
    {
        return new TargetedSaveFacts(
            transportDomainFacts.TransportLines,
            transportDomainFacts.WaitingPassengers,
            transportDomainFacts.OutsideConnections,
            transportDomainFacts.LineQueues);
    }
}

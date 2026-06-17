using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class ConnectedNodeLayoutFactsExtractor
{
    private const string ConnectedNodeTypeName = "Game.Net.ConnectedNode";

    public static ConnectedNodeLayoutFacts Extract(byte[] payload, SavePreludeSummary summary)
    {
        var connectedNodeIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, ConnectedNodeTypeName);
        if (connectedNodeIndex < 0)
        {
            return new ConnectedNodeLayoutFacts(new ReadOnlyCollection<ConnectedNodeLayoutArchetypeFact>([]));
        }

        var archetypeFacts = new List<ConnectedNodeLayoutArchetypeFact>();
        ArchetypeBufferWalker.Walk(
            payload,
            summary,
            onComponentBlock: context =>
            {
                if (context.ComponentIndex != connectedNodeIndex)
                {
                    return;
                }

                var bytesPerEntity = ConnectedNodeLayoutDecoder.TryGetBytesPerEntity(
                    context.BlockLength,
                    context.ArchetypeContext.Archetype.EntityCount);
                var flatStrideCandidate = ConnectedNodeLayoutDecoder.BuildFlatStrideCandidate(
                    context.Block,
                    context.ArchetypeContext.Archetype.EntityCount,
                    bytesPerEntity);
                var countPrefixedCandidate = ConnectedNodeLayoutDecoder.BuildCountPrefixedCandidate(
                    context.Block,
                    context.ArchetypeContext.Archetype.EntityCount);
                archetypeFacts.Add(
                    new ConnectedNodeLayoutArchetypeFact(
                        context.ArchetypeContext.Archetype.Index,
                        context.ArchetypeContext.Archetype.EntityCount,
                        context.ComponentIndex,
                        context.ComponentOrdinal,
                        context.ComponentType.TypeName,
                        context.BlockLength,
                        bytesPerEntity,
                        ConnectedNodeLayoutDecoder.BuildLeadingHex(context.Block),
                        new ReadOnlyCollection<int>(ConnectedNodeLayoutDecoder.ReadLeadingInt32Values(context.Block)),
                        ConnectedNodeLayoutDecoder.DetermineLikelyLayout(flatStrideCandidate.MatchesLayout, countPrefixedCandidate.MatchesLayout),
                        flatStrideCandidate,
                        countPrefixedCandidate));
            });

        return new ConnectedNodeLayoutFacts(
            new ReadOnlyCollection<ConnectedNodeLayoutArchetypeFact>(
                archetypeFacts
                    .OrderBy(fact => fact.ArchetypeIndex)
                    .ToList()));
    }
}

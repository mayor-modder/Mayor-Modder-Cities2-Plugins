namespace SaveInvestigator;

internal sealed record ConnectedLineCarrierFact(
    int SourceEntityIndex,
    int SourceArchetypeIndex,
    int ConnectedTargetEntityIndex,
    int LineEntityIndex);

internal static class TransportConnectedLineCarrierReader
{
    private const string ConnectedTypeName = "Game.Routes.Connected";
    private const string OwnerTypeName = "Game.Common.Owner";

    public static Dictionary<int, List<ConnectedLineCarrierFact>> ExtractByConnectedTargetEntityIndex(
        byte[] payload,
        SavePreludeSummary summary,
        IReadOnlySet<int> knownLineEntityIndexes)
    {
        var connectedComponentIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, ConnectedTypeName);
        var ownerComponentIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, OwnerTypeName);
        if (connectedComponentIndex < 0 || ownerComponentIndex < 0)
        {
            return [];
        }

        var matchesByStopEntityIndex = new Dictionary<int, List<ConnectedLineCarrierFact>>();
        ArchetypeComponentBlockContext? connectedBlock = null;
        ArchetypeComponentBlockContext? ownerBlock = null;

        ArchetypeBufferWalker.Walk(
            payload,
            summary,
            onArchetypeStarted: _ =>
            {
                connectedBlock = null;
                ownerBlock = null;
            },
            onComponentBlock: blockContext =>
            {
                if (blockContext.ComponentIndex == connectedComponentIndex)
                {
                    connectedBlock = blockContext;
                }
                else if (blockContext.ComponentIndex == ownerComponentIndex)
                {
                    ownerBlock = blockContext;
                }
            },
            onArchetypeCompleted: archetypeContext =>
            {
                if (connectedBlock?.BytesPerEntity != sizeof(int) || ownerBlock?.BytesPerEntity != sizeof(int))
                {
                    return;
                }

                for (var entityOrdinal = 0; entityOrdinal < archetypeContext.Archetype.EntityCount; entityOrdinal += 1)
                {
                    var sourceEntityIndex = archetypeContext.EntityIndexBase + entityOrdinal;
                    var connectedTargetEntityIndex = BitConverter.ToInt32(connectedBlock.Block, entityOrdinal * sizeof(int));
                    var ownerTargetEntityIndex = BitConverter.ToInt32(ownerBlock.Block, entityOrdinal * sizeof(int));
                    if (!knownLineEntityIndexes.Contains(ownerTargetEntityIndex) || connectedTargetEntityIndex < 0)
                    {
                        continue;
                    }

                    if (!matchesByStopEntityIndex.TryGetValue(connectedTargetEntityIndex, out var carrierMatches))
                    {
                        carrierMatches = [];
                        matchesByStopEntityIndex[connectedTargetEntityIndex] = carrierMatches;
                    }

                    carrierMatches.Add(
                        new ConnectedLineCarrierFact(
                            sourceEntityIndex,
                            archetypeContext.Archetype.Index,
                            connectedTargetEntityIndex,
                            ownerTargetEntityIndex));
                }
            });

        return matchesByStopEntityIndex;
    }
}

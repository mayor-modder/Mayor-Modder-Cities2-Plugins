namespace SaveInvestigator;

public static class EntityGraphLookup
{
    public static Dictionary<int, int> BuildSingleTargetMap(EntityGraphFacts entityGraphFacts, string edgeKind)
    {
        return entityGraphFacts.Edges
            .Where(edge => string.Equals(edge.EdgeKind, edgeKind, StringComparison.Ordinal))
            .GroupBy(edge => edge.SourceEntityIndex)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(edge => edge.SourceComponentIndex).ThenBy(edge => edge.TargetEntityIndex).First().TargetEntityIndex);
    }
}

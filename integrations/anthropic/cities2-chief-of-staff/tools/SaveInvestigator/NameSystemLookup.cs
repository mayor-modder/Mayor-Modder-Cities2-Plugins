namespace SaveInvestigator;

public static class NameSystemLookup
{
    public static IReadOnlyDictionary<int, NameSystemEntryFact> BuildEntryByEntityIndex(SystemTableFacts systemTableFacts)
    {
        var entriesByEntityIndex = new Dictionary<int, NameSystemEntryFact>();
        if (systemTableFacts.NameSystem is null)
        {
            return entriesByEntityIndex;
        }

        foreach (var entry in systemTableFacts.NameSystem.Entries)
        {
            if (entry.EntityIndex < 0)
            {
                continue;
            }

            entriesByEntityIndex[entry.EntityIndex] = entry;
        }

        return entriesByEntityIndex;
    }

    public static IReadOnlyDictionary<int, string> BuildValueByEntityIndex(SystemTableFacts systemTableFacts)
    {
        var valuesByEntityIndex = new Dictionary<int, string>();
        if (systemTableFacts.NameSystem is null)
        {
            return valuesByEntityIndex;
        }

        foreach (var entry in systemTableFacts.NameSystem.Entries)
        {
            if (entry.EntityIndex < 0)
            {
                continue;
            }

            valuesByEntityIndex[entry.EntityIndex] = entry.Value;
        }

        return valuesByEntityIndex;
    }
}

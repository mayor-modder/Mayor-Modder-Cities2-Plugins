using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class BuildingDomainFactsExtractor
{
    private const string BuildingTypePrefix = "Game.Buildings.";
    private const string BuildingEntityTypeName = "Game.Buildings.Building";

    private static readonly HashSet<string> StructuralBuildingTypePrefixes =
    [
        "Game.Buildings.Building",
        "Game.Buildings.Lot",
        "Game.Buildings.Extension",
        "Game.Buildings.BuildingModifier",
        "Game.Buildings.BuildingCondition",
        "Game.Buildings.SpawnLocationElement",
        "Game.Buildings.ConnectedBuilding",
        "Game.Buildings.InstalledUpgrade",
        "Game.Buildings.ModifiedServiceCoverage"
    ];

    public static BuildingDomainFacts Extract(
        SavePreludeSummary summary,
        SystemTableFacts systemTableFacts,
        EntityGraphFacts entityGraphFacts)
    {
        var ownerEntityIndexByEntityIndex = EntityGraphLookup.BuildSingleTargetMap(entityGraphFacts, "owner");
        var prefabRefValueByEntityIndex = EntityGraphLookup.BuildSingleTargetMap(entityGraphFacts, "prefab");
        var customNameByEntityIndex = NameSystemLookup.BuildEntryByEntityIndex(systemTableFacts);
        var candidates = EnumerateBuildingCandidates(summary);
        var buildingEntityIndexes = candidates
            .Select(candidate => candidate.EntityIndex)
            .ToHashSet();

        var buildings = candidates
            .Select(
                candidate =>
                {
                    var ownerEntityIndex = ownerEntityIndexByEntityIndex.TryGetValue(candidate.EntityIndex, out var ownerValue) &&
                                           buildingEntityIndexes.Contains(ownerValue)
                        ? ownerValue
                        : (int?)null;
                    var serviceComponentTypes = candidate.BuildingComponentTypes
                        .Where(typeName => !IsStructuralBuildingComponent(typeName))
                        .ToList();

                    return new BuildingDomainBuildingFact(
                        candidate.EntityIndex,
                        candidate.ArchetypeIndex,
                        candidate.EntityOrdinal,
                        ownerEntityIndex,
                        new ReadOnlyCollection<int>(
                            BuildOwnerChain(candidate.EntityIndex, ownerEntityIndexByEntityIndex, buildingEntityIndexes)),
                        prefabRefValueByEntityIndex.TryGetValue(candidate.EntityIndex, out var prefabRefValue)
                            ? prefabRefValue
                            : null,
                        customNameByEntityIndex.ContainsKey(candidate.EntityIndex),
                        customNameByEntityIndex.TryGetValue(candidate.EntityIndex, out var nameEntry)
                            ? nameEntry.Value
                            : null,
                        new ReadOnlyCollection<string>(candidate.BuildingComponentTypes),
                        new ReadOnlyCollection<string>(serviceComponentTypes));
                })
            .OrderBy(building => building.EntityIndex)
            .ToList();

        return new BuildingDomainFacts(
            new ReadOnlyCollection<BuildingDomainBuildingFact>(buildings));
    }

    private static List<BuildingCandidate> EnumerateBuildingCandidates(SavePreludeSummary summary)
    {
        var buildingEntityIndex = SerializedTypeIndexLookup.FindComponentIndex(summary.ComponentTypes, BuildingEntityTypeName);
        if (buildingEntityIndex < 0)
        {
            return [];
        }

        var results = new List<BuildingCandidate>();
        var entityIndexBase = 0;
        foreach (var archetype in summary.Archetypes)
        {
            if (!archetype.ComponentTypeIndexes.Contains(buildingEntityIndex))
            {
                entityIndexBase += archetype.EntityCount;
                continue;
            }

            var buildingComponentTypes = archetype.ComponentTypeIndexes
                .Select(index => summary.ComponentTypes[index].TypeName)
                .Where(typeName => typeName.StartsWith(BuildingTypePrefix, StringComparison.Ordinal))
                .ToList();
            for (var entityOrdinal = 0; entityOrdinal < archetype.EntityCount; entityOrdinal += 1)
            {
                results.Add(
                    new BuildingCandidate(
                        entityIndexBase + entityOrdinal,
                        archetype.Index,
                        entityOrdinal,
                        buildingComponentTypes));
            }

            entityIndexBase += archetype.EntityCount;
        }

        return results;
    }

    private static List<int> BuildOwnerChain(
        int entityIndex,
        IReadOnlyDictionary<int, int> ownerEntityIndexByEntityIndex,
        IReadOnlySet<int> buildingEntityIndexes)
    {
        var results = new List<int>();
        var seen = new HashSet<int> { entityIndex };
        var current = entityIndex;

        while (ownerEntityIndexByEntityIndex.TryGetValue(current, out var ownerEntityIndex) &&
               buildingEntityIndexes.Contains(ownerEntityIndex) &&
               seen.Add(ownerEntityIndex))
        {
            results.Add(ownerEntityIndex);
            current = ownerEntityIndex;
        }

        return results;
    }

    private static bool IsStructuralBuildingComponent(string typeName)
    {
        return StructuralBuildingTypePrefixes.Any(prefix => typeName.StartsWith(prefix, StringComparison.Ordinal));
    }

    private sealed record BuildingCandidate(
        int EntityIndex,
        int ArchetypeIndex,
        int EntityOrdinal,
        List<string> BuildingComponentTypes);
}

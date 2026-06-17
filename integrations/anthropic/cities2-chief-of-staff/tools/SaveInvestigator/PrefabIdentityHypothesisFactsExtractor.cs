using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class PrefabIdentityHypothesisFactsExtractor
{
    public static PrefabIdentityHypothesisFacts Extract(
        BuildingDomainFacts buildingDomainFacts,
        ManagedReconNotesFacts managedReconNotesFacts)
    {
        var prefabNote = managedReconNotesFacts.TypeNotes
            .First(note => string.Equals(note.TypeName, "Game.Prefabs.PrefabRef", StringComparison.Ordinal));
        var buildingGroups = buildingDomainFacts.Buildings
            .GroupBy(
                building => building.BuildingOwnerChainEntityIndexes.Count > 0
                    ? building.BuildingOwnerChainEntityIndexes[^1]
                    : building.EntityIndex)
            .ToList();
        var prefabTaggedBuildings = buildingDomainFacts.Buildings
            .Where(building => building.PrefabRefValue.HasValue)
            .ToList();
        var groupsWithDistinctPrefabRefs = buildingGroups
            .Where(
                group => group.Select(building => building.PrefabRefValue)
                    .Where(value => value.HasValue)
                    .Select(value => value!.Value)
                    .Distinct()
                    .Count() > 1)
            .ToList();

        var validationStatus = groupsWithDistinctPrefabRefs.Count > 0
            ? "confirmed"
            : prefabTaggedBuildings.Count > 0
                ? "inconclusive"
                : "disproved";

        var summary = validationStatus switch
        {
            "confirmed" => $"PrefabRef adds a distinct save-backed building identity dimension in {groupsWithDistinctPrefabRefs.Count} related building groups.",
            "inconclusive" => $"PrefabRef is present on {prefabTaggedBuildings.Count} buildings, but this sample does not yet prove a distinct identity dimension beyond existing building grouping.",
            _ => "PrefabRef did not appear on the sampled building set, so the identity hypothesis is not supported in this save view."
        };

        var evidenceNotes = new List<string>
        {
            $"managed_observation:{prefabNote.ManagedObservation}",
            $"coverage:prefab_tagged_buildings={prefabTaggedBuildings.Count}",
            $"coverage:building_groups={buildingGroups.Count}",
            $"coverage:groups_with_distinct_prefab_refs={groupsWithDistinctPrefabRefs.Count}"
        };

        foreach (var group in groupsWithDistinctPrefabRefs.Take(5))
        {
            var root = group.Key;
            var prefabRefs = group.Select(building => building.PrefabRefValue)
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            evidenceNotes.Add($"distinct_group:{root}:{string.Join(',', prefabRefs)}");
        }

        return new PrefabIdentityHypothesisFacts(
            new ReadOnlyCollection<PrefabIdentityHypothesisResultFact>(
                [
                    new PrefabIdentityHypothesisResultFact(
                        "building_prefab_identity",
                        prefabNote.TypeName,
                        validationStatus,
                        summary,
                        new ReadOnlyCollection<string>(evidenceNotes))
                ]));
    }
}

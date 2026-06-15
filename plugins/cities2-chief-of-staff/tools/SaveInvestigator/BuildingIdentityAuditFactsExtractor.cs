using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class BuildingIdentityAuditFactsExtractor
{
    public static BuildingIdentityAuditFacts Extract(
        BuildingDomainFacts buildingDomainFacts,
        EntityGraphFacts entityGraphFacts)
    {
        var buildings = buildingDomainFacts.Buildings
            .Select(
                building =>
                {
                    var baseBuildingEntityIndex = building.BuildingOwnerChainEntityIndexes.Count > 0
                        ? building.BuildingOwnerChainEntityIndexes[^1]
                        : building.EntityIndex;
                    var evidenceNotes = new List<string>();

                    if (building.HasCustomName)
                    {
                        evidenceNotes.Add("name:custom");
                    }

                    if (building.PrefabRefValue is int prefabRefValue)
                    {
                        evidenceNotes.Add($"prefab_ref:{prefabRefValue}");
                    }

                    foreach (var ownerEntityIndex in building.BuildingOwnerChainEntityIndexes)
                    {
                        evidenceNotes.Add($"owner_chain:{ownerEntityIndex}");
                    }

                    return new BuildingIdentityAuditBuildingFact(
                        building.EntityIndex,
                        baseBuildingEntityIndex,
                        building.PrefabRefValue,
                        building.HasCustomName,
                        building.CustomName,
                        "unresolved",
                        "unresolved",
                        new ReadOnlyCollection<string>(evidenceNotes.Distinct(StringComparer.Ordinal).ToList()));
                })
            .OrderBy(building => building.BaseBuildingEntityIndex)
            .ThenBy(building => building.EntityIndex)
            .ToList();

        return new BuildingIdentityAuditFacts(
            new ReadOnlyCollection<BuildingIdentityAuditBuildingFact>(buildings));
    }
}

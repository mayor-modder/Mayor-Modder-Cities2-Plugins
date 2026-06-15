using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class CityIdentityFactsExtractor
{
    public static CityIdentityFacts Extract(
        BuildingDomainFacts buildingDomainFacts,
        EntityGraphFacts entityGraphFacts,
        PrefabIdentityHypothesisFacts prefabIdentityHypothesisFacts,
        CityIdentityCarrierAuditFacts cityIdentityCarrierAuditFacts)
    {
        var buildingsByBaseEntityIndex = buildingDomainFacts.Buildings
            .GroupBy(building => building.BuildingOwnerChainEntityIndexes.Count > 0
                ? building.BuildingOwnerChainEntityIndexes[^1]
                : building.EntityIndex)
            .ToDictionary(group => group.Key, group => group.ToList());
        var attachedSources = entityGraphFacts.Edges
            .Where(edge => string.Equals(edge.EdgeKind, "attached", StringComparison.Ordinal))
            .Select(edge => edge.SourceEntityIndex)
            .ToHashSet();
        var unresolvedContextDimensions = ResolveUnresolvedContextDimensions(cityIdentityCarrierAuditFacts);
        var prefabIdentityConfirmed = prefabIdentityHypothesisFacts.Results.Any(
            result => string.Equals(result.HypothesisKey, "building_prefab_identity", StringComparison.Ordinal) &&
                      string.Equals(result.ValidationStatus, "confirmed", StringComparison.Ordinal));

        var entities = buildingDomainFacts.Buildings
            .Select(
                building =>
                {
                    var baseEntityIndex = building.BuildingOwnerChainEntityIndexes.Count > 0
                        ? building.BuildingOwnerChainEntityIndexes[^1]
                        : building.EntityIndex;
                    var family = ClassifyFamily(building);
                    var relationshipStatus = ClassifyRelationshipStatus(building, attachedSources, buildingsByBaseEntityIndex);
                    var provenContextDimensions = new HashSet<string>(StringComparer.Ordinal);
                    var evidenceNotes = new List<string> { $"family:{family}", $"relationship:{relationshipStatus}" };

                    if (building.PrefabRefValue.HasValue)
                    {
                        provenContextDimensions.Add("prefab_ref");
                        evidenceNotes.Add($"prefab_ref:{building.PrefabRefValue.Value}");
                    }

                    if (building.BuildingOwnerChainEntityIndexes.Count > 0)
                    {
                        provenContextDimensions.Add("owner_chain");
                        evidenceNotes.Add($"owner_chain_depth:{building.BuildingOwnerChainEntityIndexes.Count}");
                    }

                    if (attachedSources.Contains(building.EntityIndex))
                    {
                        provenContextDimensions.Add("attached_reference");
                        evidenceNotes.Add("attached_reference:true");
                    }

                    if (building.HasCustomName && !string.IsNullOrWhiteSpace(building.CustomName))
                    {
                        evidenceNotes.Add("name:custom");
                    }

                    if (building.ServiceComponentTypes.Count > 0)
                    {
                        evidenceNotes.Add($"service_component_count:{building.ServiceComponentTypes.Count}");
                        evidenceNotes.Add($"service_component_sample:{building.ServiceComponentTypes[0]}");
                    }

                    if (prefabIdentityConfirmed && building.PrefabRefValue.HasValue)
                    {
                        evidenceNotes.Add("prefab_identity_hypothesis:confirmed");
                    }

                    return new CityIdentityEntityFact(
                        building.EntityIndex,
                        baseEntityIndex,
                        family,
                        relationshipStatus,
                        building.PrefabRefValue,
                        building.CustomName,
                        new ReadOnlyCollection<string>(provenContextDimensions.OrderBy(value => value, StringComparer.Ordinal).ToList()),
                        new ReadOnlyCollection<string>(unresolvedContextDimensions),
                        new ReadOnlyCollection<string>(evidenceNotes));
                })
            .OrderBy(entity => entity.BaseEntityIndex)
            .ThenBy(entity => entity.EntityIndex)
            .ToList();

        return new CityIdentityFacts(
            new ReadOnlyCollection<CityIdentityEntityFact>(entities));
    }

    private static string ClassifyFamily(BuildingDomainBuildingFact building)
    {
        var componentTypes = building.BuildingComponentTypes
            .Concat(building.ServiceComponentTypes)
            .ToArray();

        if (componentTypes.Any(typeName =>
                typeName.Contains("Transport", StringComparison.Ordinal) ||
                typeName.Contains("Station", StringComparison.Ordinal) ||
                typeName.Contains("Harbor", StringComparison.Ordinal)))
        {
            return "transport_building";
        }

        if (componentTypes.Any(typeName => typeName.Contains("Residential", StringComparison.Ordinal)))
        {
            return "residential_building";
        }

        if (componentTypes.Any(typeName => typeName.Contains("Office", StringComparison.Ordinal)))
        {
            return "office_building";
        }

        if (componentTypes.Any(typeName => typeName.Contains("Commercial", StringComparison.Ordinal)))
        {
            return "commercial_building";
        }

        if (componentTypes.Any(typeName =>
                typeName.Contains("Industrial", StringComparison.Ordinal) ||
                typeName.Contains("Extractor", StringComparison.Ordinal) ||
                typeName.Contains("Storage", StringComparison.Ordinal)))
        {
            return "industrial_building";
        }

        return building.ServiceComponentTypes.Count > 0
            ? "service_building"
            : "generic_building";
    }

    private static string ClassifyRelationshipStatus(
        BuildingDomainBuildingFact building,
        IReadOnlySet<int> attachedSources,
        IReadOnlyDictionary<int, List<BuildingDomainBuildingFact>> buildingsByBaseEntityIndex)
    {
        if (building.BuildingComponentTypes.Any(typeName =>
                typeName.Contains("Extension", StringComparison.Ordinal) ||
                typeName.Contains("InstalledUpgrade", StringComparison.Ordinal)))
        {
            return "upgrade";
        }

        if (building.BuildingOwnerChainEntityIndexes.Count > 0)
        {
            return "child";
        }

        if (attachedSources.Contains(building.EntityIndex))
        {
            return "attached";
        }

        return buildingsByBaseEntityIndex.TryGetValue(building.EntityIndex, out var group) && group.Count > 1
            ? "base"
            : "base";
    }

    private static ReadOnlyCollection<string> ResolveUnresolvedContextDimensions(
        CityIdentityCarrierAuditFacts cityIdentityCarrierAuditFacts)
    {
        var unresolved = new HashSet<string>(StringComparer.Ordinal);
        foreach (var carrier in cityIdentityCarrierAuditFacts.Carriers.Where(
                     carrier => string.Equals(carrier.IdentityDimension, "context", StringComparison.Ordinal) &&
                                string.Equals(carrier.SupportStatus, "unresolved", StringComparison.Ordinal)))
        {
            switch (carrier.CarrierKey)
            {
                case "building_context_fields":
                    unresolved.Add("road_edge");
                    unresolved.Add("curve_position");
                    break;
                case "zone_blocks":
                    unresolved.Add("zone");
                    break;
                case "district_markers":
                    unresolved.Add("district");
                    break;
                default:
                    unresolved.Add(carrier.CarrierKey);
                    break;
            }
        }

        return new ReadOnlyCollection<string>(unresolved.OrderBy(value => value, StringComparer.Ordinal).ToList());
    }
}

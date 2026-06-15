using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class CityIdentityCarrierAuditFactsExtractor
{
    public static CityIdentityCarrierAuditFacts Extract(
        SavePreludeSummary summary,
        BuildingDomainFacts buildingDomainFacts,
        EntityGraphFacts entityGraphFacts)
    {
        var serializedTypeNames = summary.ComponentTypes
            .Select(component => component.TypeName)
            .ToHashSet(StringComparer.Ordinal);
        var carriers = new List<CityIdentityCarrierFact>();

        carriers.Add(BuildPrefabCarrier(serializedTypeNames, buildingDomainFacts, entityGraphFacts));
        carriers.Add(BuildBuildingRoleCarrier(buildingDomainFacts));
        carriers.Add(BuildOwnerChainCarrier(serializedTypeNames, buildingDomainFacts, entityGraphFacts));
        carriers.Add(BuildAttachedCarrier(serializedTypeNames, entityGraphFacts));

        if (SerializedTypeMatcher.Contains(serializedTypeNames, "Game.Buildings.Building"))
        {
            carriers.Add(
                new CityIdentityCarrierFact(
                    "building_context_fields",
                    "context",
                    "unresolved",
                    "Game.Buildings.Building is present, so road-edge or curve-position context may exist, but this dimension is not yet decoded into reusable city identity facts.",
                    new ReadOnlyCollection<string>(
                        [
                            "component_present:true",
                            "context_candidates:road_edge,curve_position"
                        ])));
        }

        if (SerializedTypeMatcher.Contains(serializedTypeNames, "Game.Zones.Block"))
        {
            carriers.Add(
                new CityIdentityCarrierFact(
                    "zone_blocks",
                    "context",
                    "unresolved",
                    "Zone-block structure is present in the save, but it is not yet connected back into reusable entity identity or location context.",
                    new ReadOnlyCollection<string>(
                        [
                            "component_present:true",
                            "context_candidates:zone"
                        ])));
        }

        var districtTypeNames = serializedTypeNames
            .Where(typeName => typeName.Contains("District", StringComparison.Ordinal))
            .OrderBy(typeName => typeName, StringComparer.Ordinal)
            .ToList();
        if (districtTypeNames.Count > 0)
        {
            carriers.Add(
                new CityIdentityCarrierFact(
                    "district_markers",
                    "context",
                    "unresolved",
                    "District-related serialized types are present, but district identity is not yet promoted into the city understanding layer.",
                    new ReadOnlyCollection<string>(
                        [
                            $"carrier_types:{string.Join(" | ", districtTypeNames.Take(3))}"
                        ])));
        }

        return new CityIdentityCarrierAuditFacts(
            new ReadOnlyCollection<CityIdentityCarrierFact>(
                carriers
                    .OrderBy(carrier => carrier.IdentityDimension, StringComparer.Ordinal)
                    .ThenBy(carrier => carrier.CarrierKey, StringComparer.Ordinal)
                    .ToList()));
    }

    private static CityIdentityCarrierFact BuildPrefabCarrier(
        IReadOnlySet<string> serializedTypeNames,
        BuildingDomainFacts buildingDomainFacts,
        EntityGraphFacts entityGraphFacts)
    {
        var prefabTaggedBuildingCount = buildingDomainFacts.Buildings.Count(building => building.PrefabRefValue.HasValue);
        var referenceComponentPresent = entityGraphFacts.ReferenceComponents.Any(
            component => SerializedTypeMatcher.Matches(component.TypeName, "Game.Prefabs.PrefabRef"));
        var componentPresent = SerializedTypeMatcher.Contains(serializedTypeNames, "Game.Prefabs.PrefabRef");
        var supportStatus = prefabTaggedBuildingCount > 0 && referenceComponentPresent
            ? "decoded"
            : componentPresent
                ? "partial"
                : "unresolved";
        var summary = supportStatus switch
        {
            "decoded" => $"PrefabRef is already a live building identity carrier on {prefabTaggedBuildingCount} building entities.",
            "partial" => "PrefabRef is present, but the current decoded building set does not yet prove it as a live building identity carrier.",
            _ => "PrefabRef is not yet visible in the current save-facing identity layer."
        };

        return new CityIdentityCarrierFact(
            "prefab_ref",
            "prefab",
            supportStatus,
            summary,
            new ReadOnlyCollection<string>(
                [
                    $"component_present:{componentPresent.ToString().ToLowerInvariant()}",
                    $"reference_component_present:{referenceComponentPresent.ToString().ToLowerInvariant()}",
                    $"prefab_tagged_buildings:{prefabTaggedBuildingCount}"
                ]));
    }

    private static CityIdentityCarrierFact BuildBuildingRoleCarrier(BuildingDomainFacts buildingDomainFacts)
    {
        var distinctServiceComponentTypes = buildingDomainFacts.Buildings
            .SelectMany(building => building.ServiceComponentTypes)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(typeName => typeName, StringComparer.Ordinal)
            .ToList();
        var supportStatus = distinctServiceComponentTypes.Count > 0 ? "partial" : "unresolved";
        var summary = supportStatus == "partial"
            ? $"Building service components currently provide broad role clues across {distinctServiceComponentTypes.Count} distinct service families."
            : "No building service-component clues were available in the current building sample.";

        return new CityIdentityCarrierFact(
            "building_role_components",
            "building_role",
            supportStatus,
            summary,
            new ReadOnlyCollection<string>(
                [
                    $"distinct_service_component_types:{distinctServiceComponentTypes.Count}",
                    $"sample_types:{string.Join(" | ", distinctServiceComponentTypes.Take(3))}"
                ]));
    }

    private static CityIdentityCarrierFact BuildOwnerChainCarrier(
        IReadOnlySet<string> serializedTypeNames,
        BuildingDomainFacts buildingDomainFacts,
        EntityGraphFacts entityGraphFacts)
    {
        var buildingsWithOwnerChain = buildingDomainFacts.Buildings.Count(
            building => building.BuildingOwnerChainEntityIndexes.Count > 0);
        var buildingEntityIndexes = buildingDomainFacts.Buildings
            .Select(building => building.EntityIndex)
            .ToHashSet();
        var buildingOwnerEdgeCount = entityGraphFacts.Edges.Count(
            edge => string.Equals(edge.EdgeKind, "owner", StringComparison.Ordinal) &&
                    buildingEntityIndexes.Contains(edge.SourceEntityIndex));
        var componentPresent = SerializedTypeMatcher.Contains(serializedTypeNames, "Game.Common.Owner");
        var supportStatus = buildingsWithOwnerChain > 0 && buildingOwnerEdgeCount > 0
            ? "decoded"
            : componentPresent
                ? "partial"
                : "unresolved";
        var summary = supportStatus switch
        {
            "decoded" => $"Owner chains are already decoded for {buildingsWithOwnerChain} building entities, with {buildingOwnerEdgeCount} building-local owner edges visible in the entity graph.",
            "partial" => "Owner structure is present, but the current identity layer does not yet decode enough owner chains to treat it as a stable relationship carrier everywhere.",
            _ => "Owner-chain relationships are not yet visible in the current identity layer."
        };

        return new CityIdentityCarrierFact(
            "owner_chain",
            "relationship",
            supportStatus,
            summary,
            new ReadOnlyCollection<string>(
                [
                    $"component_present:{componentPresent.ToString().ToLowerInvariant()}",
                    $"building_owner_edges:{buildingOwnerEdgeCount}",
                    $"buildings_with_owner_chain:{buildingsWithOwnerChain}"
                ]));
    }

    private static CityIdentityCarrierFact BuildAttachedCarrier(
        IReadOnlySet<string> serializedTypeNames,
        EntityGraphFacts entityGraphFacts)
    {
        var attachedEdgeCount = entityGraphFacts.Edges.Count(edge => string.Equals(edge.EdgeKind, "attached", StringComparison.Ordinal));
        var componentPresent = SerializedTypeMatcher.Contains(serializedTypeNames, "Game.Objects.Attached");
        var supportStatus = attachedEdgeCount > 0
            ? "partial"
            : componentPresent
                ? "partial"
                : "unresolved";
        var summary = attachedEdgeCount > 0
            ? $"Attached references are present in the entity graph ({attachedEdgeCount} edges), but they are not yet promoted into the generic city identity layer."
            : componentPresent
                ? "Attached references are present in the save, but the current identity layer does not yet reuse them."
                : "Attached-reference carriers are not yet visible in the current save view.";

        return new CityIdentityCarrierFact(
            "attached_entities",
            "relationship",
            supportStatus,
            summary,
            new ReadOnlyCollection<string>(
                [
                    $"component_present:{componentPresent.ToString().ToLowerInvariant()}",
                    $"attached_edges:{attachedEdgeCount}"
                ]));
    }
}

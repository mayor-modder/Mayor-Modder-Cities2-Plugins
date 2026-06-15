using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class CityContextPromotionFactsExtractor
{
    private const string BuildingTypeName = "Game.Buildings.Building";
    private const string DistrictTypeName = "Game.Areas.CurrentDistrict";

    public static CityContextPromotionFacts Extract(
        SavePreludeSummary summary,
        BuildingDomainFacts buildingDomainFacts,
        CityIdentityFacts cityIdentityFacts,
        CityIdentityCarrierAuditFacts cityIdentityCarrierAuditFacts)
    {
        var archetypeComponentTypesByIndex = summary.Archetypes.ToDictionary(
            archetype => archetype.Index,
            archetype => archetype.ComponentTypeIndexes
                .Select(componentIndex => summary.ComponentTypes[componentIndex].TypeName)
                .ToArray());
        var buildingIndexByEntityIndex = buildingDomainFacts.Buildings.ToDictionary(building => building.EntityIndex);
        var entityFacts = new List<CityContextPromotionEntityFact>();
        var dimensionEntityIndexes = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal)
        {
            ["district"] = [],
            ["network"] = [],
            ["zone"] = [],
            ["road_edge"] = []
        };

        foreach (var identityEntity in cityIdentityFacts.Entities)
        {
            if (!buildingIndexByEntityIndex.TryGetValue(identityEntity.EntityIndex, out var building) ||
                !archetypeComponentTypesByIndex.TryGetValue(building.ArchetypeIndex, out var componentTypes))
            {
                continue;
            }

            var provenContextDimensions = new HashSet<string>(identityEntity.ProvenContextDimensions, StringComparer.Ordinal);
            var evidenceNotes = new HashSet<string>(identityEntity.EvidenceNotes, StringComparer.Ordinal);

            if (componentTypes.Any(typeName => SerializedTypeMatcher.Matches(typeName, DistrictTypeName)))
            {
                provenContextDimensions.Add("district");
                dimensionEntityIndexes["district"].Add(identityEntity.EntityIndex);
                evidenceNotes.Add("context_component:Game.Areas.CurrentDistrict");
            }

            if (componentTypes.Any(typeName => typeName.StartsWith("Game.Net.", StringComparison.Ordinal)))
            {
                provenContextDimensions.Add("network");
                dimensionEntityIndexes["network"].Add(identityEntity.EntityIndex);
                evidenceNotes.Add("context_component:Game.Net.*");
            }

            if (componentTypes.Any(typeName => typeName.StartsWith("Game.Zones.", StringComparison.Ordinal)))
            {
                provenContextDimensions.Add("zone");
                dimensionEntityIndexes["zone"].Add(identityEntity.EntityIndex);
                evidenceNotes.Add("context_component:Game.Zones.*");
            }

            if (componentTypes.Any(typeName => SerializedTypeMatcher.Matches(typeName, "Game.Zones.CurvePosition")))
            {
                provenContextDimensions.Add("road_edge");
                dimensionEntityIndexes["road_edge"].Add(identityEntity.EntityIndex);
                evidenceNotes.Add("context_component:Game.Zones.CurvePosition");
            }

            var remainingContextDimensions = identityEntity.UnresolvedContextDimensions
                .Where(dimension => !provenContextDimensions.Contains(dimension))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(dimension => dimension, StringComparer.Ordinal)
                .ToList();

            entityFacts.Add(
                new CityContextPromotionEntityFact(
                    identityEntity.EntityIndex,
                    identityEntity.BaseEntityIndex,
                    identityEntity.Family,
                    new ReadOnlyCollection<string>(provenContextDimensions.OrderBy(dimension => dimension, StringComparer.Ordinal).ToList()),
                    new ReadOnlyCollection<string>(remainingContextDimensions),
                    new ReadOnlyCollection<string>(evidenceNotes.OrderBy(note => note, StringComparer.Ordinal).ToList())));
        }

        var dimensions = BuildDimensions(
            buildingDomainFacts.Buildings.Count,
            summary.ComponentTypes,
            cityIdentityCarrierAuditFacts,
            dimensionEntityIndexes);

        return new CityContextPromotionFacts(
            new ReadOnlyCollection<CityContextPromotionDimensionFact>(dimensions),
            new ReadOnlyCollection<CityContextPromotionEntityFact>(
                entityFacts
                    .OrderBy(entity => entity.BaseEntityIndex)
                    .ThenBy(entity => entity.EntityIndex)
                    .ToList()));
    }

    private static List<CityContextPromotionDimensionFact> BuildDimensions(
        int totalBuildings,
        IReadOnlyList<ComponentTypeSummary> componentTypes,
        CityIdentityCarrierAuditFacts cityIdentityCarrierAuditFacts,
        IReadOnlyDictionary<string, HashSet<int>> dimensionEntityIndexes)
    {
        var allComponentTypeNames = componentTypes.Select(component => component.TypeName).ToArray();

        return
        [
            BuildDimensionFact(
                "district",
                dimensionEntityIndexes["district"],
                totalBuildings,
                allComponentTypeNames,
                cityIdentityCarrierAuditFacts,
                "Game.Areas.CurrentDistrict",
                "District context is proven structurally from building archetypes that carry Game.Areas.CurrentDistrict.",
                "District-related save types are present, but no building archetype has yet proved district context directly."),
            BuildDimensionFact(
                "network",
                dimensionEntityIndexes["network"],
                totalBuildings,
                allComponentTypeNames,
                cityIdentityCarrierAuditFacts,
                "Game.Net.",
                "Network context is proven structurally from building archetypes that carry Game.Net.* components.",
                "Network-facing save types are present, but no building archetype has yet proved reusable network context directly."),
            BuildDimensionFact(
                "zone",
                dimensionEntityIndexes["zone"],
                totalBuildings,
                allComponentTypeNames,
                cityIdentityCarrierAuditFacts,
                "Game.Zones.",
                "Zone context is proven structurally from building archetypes that carry Game.Zones.* components.",
                "Zone-related save types are present, but no building archetype has yet proved reusable zone context directly."),
            BuildDimensionFact(
                "road_edge",
                dimensionEntityIndexes["road_edge"],
                totalBuildings,
                allComponentTypeNames,
                cityIdentityCarrierAuditFacts,
                "Game.Zones.CurvePosition",
                "Road-edge context is proven structurally from building archetypes that carry Game.Zones.CurvePosition.",
                "Road-edge carriers remain unresolved because no building archetype has yet proved a direct edge/curve-position context dimension.")
        ];
    }

    private static CityContextPromotionDimensionFact BuildDimensionFact(
        string dimension,
        IReadOnlySet<int> entityIndexes,
        int totalBuildings,
        IReadOnlyList<string> componentTypeNames,
        CityIdentityCarrierAuditFacts cityIdentityCarrierAuditFacts,
        string evidencePrefix,
        string provenSummary,
        string unresolvedSummary)
    {
        var supportStatus = entityIndexes.Count > 0 ? "proven" : "unresolved";
        var evidenceNotes = new HashSet<string>(StringComparer.Ordinal)
        {
            $"entities_with_dimension:{entityIndexes.Count}",
            $"total_buildings:{totalBuildings}"
        };

        var componentEvidenceCount = componentTypeNames.Count(
            typeName => evidencePrefix.EndsWith(".", StringComparison.Ordinal)
                ? typeName.StartsWith(evidencePrefix, StringComparison.Ordinal)
                : SerializedTypeMatcher.Matches(typeName, evidencePrefix));
        if (componentEvidenceCount > 0)
        {
            evidenceNotes.Add($"matching_component_types:{componentEvidenceCount}");
        }

        foreach (var carrier in cityIdentityCarrierAuditFacts.Carriers.Where(
                     carrier => string.Equals(carrier.IdentityDimension, "context", StringComparison.Ordinal) &&
                                CarrierSupportsDimension(carrier.CarrierKey, dimension)))
        {
            evidenceNotes.Add($"carrier:{carrier.CarrierKey}:{carrier.SupportStatus}");
        }

        var summary = supportStatus == "proven"
            ? $"{provenSummary} Proven on {entityIndexes.Count} of {totalBuildings} building entities."
            : unresolvedSummary;

        return new CityContextPromotionDimensionFact(
            dimension,
            supportStatus,
            entityIndexes.Count,
            summary,
            new ReadOnlyCollection<string>(evidenceNotes.OrderBy(note => note, StringComparer.Ordinal).ToList()));
    }

    private static bool CarrierSupportsDimension(string carrierKey, string dimension)
    {
        return (carrierKey, dimension) switch
        {
            ("district_markers", "district") => true,
            ("zone_blocks", "zone") => true,
            ("building_context_fields", "road_edge") => true,
            ("building_context_fields", "network") => true,
            _ => false
        };
    }
}

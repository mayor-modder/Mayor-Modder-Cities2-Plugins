using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class SaveCoverageFactsExtractor
{
    private static readonly HashSet<string> PartiallyDecodedComponentFamilies =
    [
        "Game.Buildings",
        "Game.Citizens",
        "Game.Companies",
        "Game.Common",
        "Game.Economy",
        "Game.Net",
        "Game.Routes",
        "Game.UI"
    ];

    private static readonly HashSet<string> BaseGameAssemblyNames =
    [
        "Game",
        "Colossal",
        "Unity.Collections",
        "Unity.Entities",
        "Unity.Mathematics",
        "UnityEngine.CoreModule"
    ];

    public static SaveCoverageFacts Extract(
        SerializerCatalogFacts serializerCatalogFacts,
        SystemTableFacts systemTableFacts,
        IReadOnlyCollection<TypeResolutionSummary> componentTypeResolutions,
        IReadOnlyCollection<TypeResolutionSummary> systemTypeResolutions)
    {
        var componentResolutionByTypeName = componentTypeResolutions.ToDictionary(item => item.TypeName, StringComparer.Ordinal);
        var systemResolutionByTypeName = systemTypeResolutions.ToDictionary(item => item.TypeName, StringComparer.Ordinal);

        var componentFamilies = serializerCatalogFacts.Components
            .GroupBy(component => GetFamilyName(component.TypeName))
            .Select(
                group =>
                {
                    var familyName = group.Key;
                    var status = ResolveComponentFamilyStatus(group, componentResolutionByTypeName);
                    return new CoverageFamilyFact(
                        "component",
                        familyName,
                        status,
                        group.Count(),
                        status is "fully_decoded" ? group.Count() : 0,
                        new ReadOnlyCollection<string>(
                            group
                                .Select(component => component.TypeName)
                                .OrderBy(typeName => typeName, StringComparer.Ordinal)
                                .Take(3)
                                .ToList()),
                        BuildNotes(status, familyName));
                })
            .OrderBy(family => family.FamilyName, StringComparer.Ordinal)
            .ToList();

        var systemFamilies = systemTableFacts.ReviewedSystems
            .GroupBy(system => GetFamilyName(system.TypeName))
            .Select(
                group =>
                {
                    var familyName = group.Key;
                    var status = ResolveSystemFamilyStatus(group, systemResolutionByTypeName);
                    var decodedTypeCount = group.Count(system => string.Equals(system.Resolution, "decoded", StringComparison.Ordinal));
                    return new CoverageFamilyFact(
                        "system",
                        familyName,
                        status,
                        group.Count(),
                        decodedTypeCount,
                        new ReadOnlyCollection<string>(
                            group
                                .Select(system => system.TypeName)
                                .OrderBy(typeName => typeName, StringComparer.Ordinal)
                                .Take(3)
                                .ToList()),
                        BuildNotes(status, familyName));
                })
            .OrderBy(family => family.FamilyName, StringComparer.Ordinal)
            .ToList();

        var openItems = new List<CoverageOpenItemFact>
        {
            new("runtime_synthesized", "transport", "Default-generated transit names are runtime-synthesized when they are absent from Game.UI.NameSystem."),
            new("unresolved", "prefabs", "PrefabRef values are persisted and captured, but the prefab-system table is still unresolved."),
            new("unresolved", "economy", "Game.Economy.Resources and Game.Companies.CurrentTrading are structurally present, but their numeric payload semantics are not decoded yet.")
        };

        return new SaveCoverageFacts(
            new ReadOnlyCollection<CoverageFamilyFact>(componentFamilies),
            new ReadOnlyCollection<CoverageFamilyFact>(systemFamilies),
            new ReadOnlyCollection<CoverageOpenItemFact>(openItems));
    }

    private static string ResolveComponentFamilyStatus(
        IGrouping<string, SerializerCatalogComponentFact> group,
        IReadOnlyDictionary<string, TypeResolutionSummary> componentResolutionByTypeName)
    {
        if (IsModOwnedFamily(
                group.Select(component => component.TypeName),
                componentResolutionByTypeName))
        {
            return "mod_owned";
        }

        return PartiallyDecodedComponentFamilies.Contains(group.Key)
            ? "partially_decoded"
            : "unresolved";
    }

    private static string ResolveSystemFamilyStatus(
        IGrouping<string, SystemTableReviewFact> group,
        IReadOnlyDictionary<string, TypeResolutionSummary> systemResolutionByTypeName)
    {
        if (IsModOwnedFamily(
                group.Select(system => system.TypeName),
                systemResolutionByTypeName))
        {
            return "mod_owned";
        }

        var decodedCount = group.Count(system => string.Equals(system.Resolution, "decoded", StringComparison.Ordinal));
        if (decodedCount == group.Count())
        {
            return "fully_decoded";
        }

        if (decodedCount > 0)
        {
            return "partially_decoded";
        }

        return "unresolved";
    }

    private static bool IsModOwnedFamily(
        IEnumerable<string> typeNames,
        IReadOnlyDictionary<string, TypeResolutionSummary> resolutionsByTypeName)
    {
        var anyAssemblyHint = false;
        foreach (var typeName in typeNames)
        {
            var assemblyName = TryGetAssemblyName(typeName, resolutionsByTypeName);
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                continue;
            }

            anyAssemblyHint = true;
            if (BaseGameAssemblyNames.Contains(assemblyName))
            {
                return false;
            }

            if (assemblyName.StartsWith("Game.", StringComparison.Ordinal) ||
                assemblyName.StartsWith("Colossal.", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return anyAssemblyHint;
    }

    private static string? TryGetAssemblyName(
        string serializedTypeName,
        IReadOnlyDictionary<string, TypeResolutionSummary> resolutionsByTypeName)
    {
        if (resolutionsByTypeName.TryGetValue(serializedTypeName, out var resolution) &&
            resolution.Resolved &&
            !string.IsNullOrWhiteSpace(resolution.ResolvedAssemblyName))
        {
            return resolution.ResolvedAssemblyName;
        }

        var parts = serializedTypeName.Split(',');
        return parts.Length >= 2
            ? parts[1].Trim()
            : null;
    }

    private static string GetFamilyName(string serializedTypeName)
    {
        var fullTypeName = serializedTypeName.Split(',')[0].Trim();
        var segments = fullTypeName.Split('.');
        return segments.Length >= 2
            ? string.Join('.', segments.Take(2))
            : fullTypeName;
    }

    private static string? BuildNotes(string status, string familyName)
    {
        return status switch
        {
            "partially_decoded" => $"{familyName} has evidence-backed extraction coverage, but not every persisted payload in the family is decoded yet.",
            "mod_owned" => $"{familyName} resolves to a non-vanilla assembly and is classified separately from base-game save coverage.",
            _ => null
        };
    }
}

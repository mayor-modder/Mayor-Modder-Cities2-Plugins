using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class SaveInvestigationReportBuilder
{
    private static readonly (string Key, string TypeName)[] HighlightTargets =
    [
        ("citizens", "Game.Citizens.Citizen, Game"),
        ("households", "Game.Citizens.Household, Game"),
        ("workers", "Game.Citizens.Worker, Game"),
        ("students", "Game.Citizens.Student, Game"),
        ("vehicles", "Game.Vehicles.Vehicle, Game"),
        ("parked_cars", "Game.Vehicles.ParkedCar, Game"),
        ("buildings", "Game.Buildings.Building, Game"),
        ("transport_lines", "Game.Routes.TransportLine, Game"),
        ("outside_connections", "Game.Net.OutsideConnection, Game"),
        ("zone_blocks", "Game.Zones.Block, Game"),
        ("transport_companies", "Game.Companies.TransportCompany, Game")
    ];

    public static SaveInvestigationReport Build(SavePreludeSummary summary)
    {
        var componentCoverage = new int[summary.ComponentTypes.Count];

        foreach (var archetype in summary.Archetypes)
        {
            foreach (var componentIndex in archetype.ComponentTypeIndexes)
            {
                if (componentIndex >= 0 && componentIndex < componentCoverage.Length)
                {
                    componentCoverage[componentIndex] += archetype.EntityCount;
                }
            }
        }

        var topComponentCoverage = summary.ComponentTypes
            .Select(component => new ComponentCoverageSummary(
                component.Index,
                component.TypeName,
                componentCoverage[component.Index]))
            .OrderByDescending(component => component.EntityCoverage)
            .ThenBy(component => component.TypeName, StringComparer.Ordinal)
            .ToList();

        var topArchetypes = summary.Archetypes
            .Select(archetype => new ArchetypeReport(
                archetype.Index,
                archetype.EntityCount,
                new ReadOnlyCollection<string>(
                    archetype.ComponentTypeIndexes
                        .Select(componentIndex => ResolveComponentTypeName(summary.ComponentTypes, componentIndex))
                        .ToList())))
            .OrderByDescending(archetype => archetype.EntityCount)
            .ThenBy(archetype => archetype.Index)
            .ToList();
        var highlightMetrics = HighlightTargets
            .Select(target =>
            {
                var component = summary.ComponentTypes.FirstOrDefault(
                    item => item.TypeName.StartsWith(target.TypeName, StringComparison.Ordinal));
                return component is null
                    ? null
                    : new HighlightMetric(target.Key, component.TypeName, componentCoverage[component.Index]);
            })
            .Where(metric => metric is not null)
            .Cast<HighlightMetric>()
            .OrderBy(metric => metric.Key, StringComparer.Ordinal)
            .ToList();

        return new SaveInvestigationReport(
            summary.Archetypes.Sum(archetype => archetype.EntityCount),
            new ReadOnlyCollection<HighlightMetric>(highlightMetrics),
            new ReadOnlyCollection<ComponentCoverageSummary>(topComponentCoverage),
            new ReadOnlyCollection<ArchetypeReport>(topArchetypes));
    }

    private static string ResolveComponentTypeName(
        IReadOnlyList<ComponentTypeSummary> componentTypes,
        int componentIndex)
    {
        if (componentIndex < 0 || componentIndex >= componentTypes.Count)
        {
            return $"<unknown:{componentIndex}>";
        }

        return componentTypes[componentIndex].TypeName;
    }
}

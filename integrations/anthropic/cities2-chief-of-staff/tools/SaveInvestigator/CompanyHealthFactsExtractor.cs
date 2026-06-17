using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class CompanyHealthFactsExtractor
{
    public static CompanyHealthFacts Extract(
        EconomyDomainFacts economyDomainFacts,
        CityIdentityFacts cityIdentityFacts,
        LaborDomainFacts laborDomainFacts,
        PopulationLaborSemanticsFacts populationLaborSemanticsFacts)
    {
        var summary = economyDomainFacts.Summary;
        var officeBuildingCandidates = cityIdentityFacts.Entities.Count(
            entity => string.Equals(entity.Family, "office_building", StringComparison.Ordinal));
        var commercialBuildingCandidates = cityIdentityFacts.Entities.Count(
            entity => string.Equals(entity.Family, "commercial_building", StringComparison.Ordinal));
        var industrialBuildingCandidates = cityIdentityFacts.Entities.Count(
            entity => string.Equals(entity.Family, "industrial_building", StringComparison.Ordinal));
        var travelActivity = populationLaborSemanticsFacts.Groups.FirstOrDefault(
            group => string.Equals(group.GroupKey, "travel_activity_context", StringComparison.Ordinal));
        var blockers = new List<string>
        {
            "profitability, sector health, and business stress semantics remain unresolved"
        };
        blockers.AddRange(
            laborDomainFacts.RemainingBlockers
                .Where(blocker => blocker.Contains("job", StringComparison.OrdinalIgnoreCase)));

        return new CompanyHealthFacts(
            summary.CompanyEntities,
            summary.TransportCompanies,
            summary.ResourceTaggedEntities,
            summary.OutsideConnections,
            officeBuildingCandidates,
            commercialBuildingCandidates,
            industrialBuildingCandidates,
            "unresolved",
            "partial",
            $"Company/economy facts currently cover {summary.CompanyEntities} companies, {summary.TransportCompanies} transport companies, {summary.ResourceTaggedEntities} resource-tagged entities, {officeBuildingCandidates} office building candidates, {commercialBuildingCandidates} commercial building candidates, {industrialBuildingCandidates} industrial building candidates, and {summary.OutsideConnections} outside connections. Labor context is {travelActivity?.SupportStatus ?? "unresolved"}, but company-side hiring semantics remain indirect.",
            new ReadOnlyCollection<string>(
                [
                    $"coverage:companies={summary.CompanyEntities}",
                    $"coverage:transport_companies={summary.TransportCompanies}",
                    $"coverage:resource_entities={summary.ResourceTaggedEntities}",
                    $"coverage:office_building_candidates={officeBuildingCandidates}",
                    $"coverage:commercial_building_candidates={commercialBuildingCandidates}",
                    $"coverage:industrial_building_candidates={industrialBuildingCandidates}",
                    $"coverage:outside_connections={summary.OutsideConnections}",
                    $"labor:job_pressure={laborDomainFacts.EvidenceNotes.FirstOrDefault(note => note.StartsWith("carrier:job_pressure=", StringComparison.Ordinal))?.Split('=')[1] ?? "unresolved"}",
                    $"labor:travel_activity_context={travelActivity?.SupportStatus ?? "unresolved"}"
                ]),
            new ReadOnlyCollection<string>(blockers.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList()));
    }

    public static CompanyHealthFacts Extract(
        EconomyDomainFacts economyDomainFacts,
        CityIdentityFacts cityIdentityFacts)
    {
        return Extract(
            economyDomainFacts,
            cityIdentityFacts,
            new LaborDomainFacts(
                0,
                0,
                0,
                0,
                "partial",
                string.Empty,
                new ReadOnlyCollection<string>([]),
                new ReadOnlyCollection<string>([])),
            new PopulationLaborSemanticsFacts(
                new ReadOnlyCollection<PopulationLaborSemanticGroupFact>([]),
                new ReadOnlyCollection<string>([])));
    }
}

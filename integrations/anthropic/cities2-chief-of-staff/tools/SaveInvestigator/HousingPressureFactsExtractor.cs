using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class HousingPressureFactsExtractor
{
    public static HousingPressureFacts Extract(
        CityIdentityFacts cityIdentityFacts,
        PopulationDomainFacts populationDomainFacts,
        PopulationLaborSemanticsFacts populationLaborSemanticsFacts)
    {
        var totalBuildings = cityIdentityFacts.Entities.Count;
        var namedBuildings = cityIdentityFacts.Entities.Count(entity => !string.IsNullOrWhiteSpace(entity.DisplayName));
        var residentialBuildingCandidates = cityIdentityFacts.Entities.Count(
            entity => string.Equals(entity.Family, "residential_building", StringComparison.Ordinal));
        var totalHouseholds = populationDomainFacts.HouseholdSummary.TotalHouseholds;
        var specialCaseHouseholds = populationDomainFacts.HouseholdSummary.CommuterHouseholds +
                                    populationDomainFacts.HouseholdSummary.TouristHouseholds +
                                    populationDomainFacts.HouseholdSummary.HomelessHouseholds;
        var householdRoleStructure = populationLaborSemanticsFacts.Groups.FirstOrDefault(
            group => string.Equals(group.GroupKey, "household_role_structure", StringComparison.Ordinal));
        var blockers = new List<string>();

        if (totalBuildings == 0)
        {
            blockers.Add("building identity coverage is too thin to describe housing pressure");
        }
        else
        {
            blockers.Add("residential occupancy and address-level housing pressure remain unresolved");
        }

        blockers.AddRange(populationLaborSemanticsFacts.RemainingBlockers);

        return new HousingPressureFacts(
            totalHouseholds,
            totalBuildings,
            namedBuildings,
            residentialBuildingCandidates,
            totalBuildings == 0 ? "insufficient" : "partial",
            $"Housing pressure currently reflects {totalHouseholds} households across {totalBuildings} decoded building records, with {residentialBuildingCandidates} residential building candidates. Household structure is {householdRoleStructure?.SupportStatus ?? "unresolved"}, and special-case households total {specialCaseHouseholds}.",
            new ReadOnlyCollection<string>(
                [
                    $"coverage:households={totalHouseholds}",
                    $"coverage:identity_buildings={totalBuildings}",
                    $"coverage:identity_named_buildings={namedBuildings}",
                    $"coverage:identity_residential_buildings={residentialBuildingCandidates}",
                    $"semantics:household_role_structure={householdRoleStructure?.SupportStatus ?? "unresolved"}",
                    $"semantics:special_case_households={specialCaseHouseholds}"
                ]),
            new ReadOnlyCollection<string>(blockers.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList()));
    }

    public static HousingPressureFacts Extract(
        CityIdentityFacts cityIdentityFacts,
        PopulationDomainFacts populationDomainFacts)
    {
        return Extract(
            cityIdentityFacts,
            populationDomainFacts,
            new PopulationLaborSemanticsFacts(
                new ReadOnlyCollection<PopulationLaborSemanticGroupFact>([]),
                new ReadOnlyCollection<string>([])));
    }
}

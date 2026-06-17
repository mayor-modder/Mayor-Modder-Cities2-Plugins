using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class PopulationDomainFactsExtractor
{
    private const string CitizenTypeName = "Game.Citizens.Citizen";
    private const string HouseholdMemberTypeName = "Game.Citizens.HouseholdMember";
    private const string WorkerTypeName = "Game.Citizens.Worker";
    private const string StudentTypeName = "Game.Citizens.Student";
    private const string CurrentBuildingTypeName = "Game.Citizens.CurrentBuilding";
    private const string CurrentTransportTypeName = "Game.Citizens.CurrentTransport";
    private const string TravelPurposeTypeName = "Game.Citizens.TravelPurpose";
    private const string HouseholdTypeName = "Game.Citizens.Household";
    private const string CommuterHouseholdTypeName = "Game.Citizens.CommuterHousehold";
    private const string TouristHouseholdTypeName = "Game.Citizens.TouristHousehold";
    private const string HomelessHouseholdTypeName = "Game.Citizens.HomelessHousehold";

    public static PopulationDomainFacts Extract(SavePreludeSummary summary)
    {
        var citizenArchetypes = new List<CitizenArchetypePopulationFact>();
        var householdArchetypes = new List<HouseholdArchetypePopulationFact>();

        foreach (var archetype in summary.Archetypes)
        {
            var componentTypes = archetype.ComponentTypeIndexes
                .Select(index => summary.ComponentTypes[index].TypeName)
                .ToHashSet(StringComparer.Ordinal);

            if (SerializedTypeMatcher.Contains(componentTypes, CitizenTypeName))
            {
                citizenArchetypes.Add(
                    new CitizenArchetypePopulationFact(
                        archetype.Index,
                        archetype.EntityCount,
                        SerializedTypeMatcher.Contains(componentTypes, HouseholdMemberTypeName),
                        SerializedTypeMatcher.Contains(componentTypes, WorkerTypeName),
                        SerializedTypeMatcher.Contains(componentTypes, StudentTypeName),
                        SerializedTypeMatcher.Contains(componentTypes, CurrentBuildingTypeName),
                        SerializedTypeMatcher.Contains(componentTypes, CurrentTransportTypeName),
                        SerializedTypeMatcher.Contains(componentTypes, TravelPurposeTypeName)));
            }

            if (SerializedTypeMatcher.Contains(componentTypes, HouseholdTypeName))
            {
                householdArchetypes.Add(
                    new HouseholdArchetypePopulationFact(
                        archetype.Index,
                        archetype.EntityCount,
                        SerializedTypeMatcher.Contains(componentTypes, CommuterHouseholdTypeName),
                        SerializedTypeMatcher.Contains(componentTypes, TouristHouseholdTypeName),
                        SerializedTypeMatcher.Contains(componentTypes, HomelessHouseholdTypeName)));
            }
        }

        var totalCitizens = citizenArchetypes.Sum(archetype => archetype.EntityCount);
        var householdMembers = citizenArchetypes
            .Where(archetype => archetype.HasHouseholdMember)
            .Sum(archetype => archetype.EntityCount);
        var workerCitizens = citizenArchetypes
            .Where(archetype => archetype.HasWorker)
            .Sum(archetype => archetype.EntityCount);
        var studentCitizens = citizenArchetypes
            .Where(archetype => archetype.HasStudent)
            .Sum(archetype => archetype.EntityCount);
        var workerStudents = citizenArchetypes
            .Where(archetype => archetype.HasWorker && archetype.HasStudent)
            .Sum(archetype => archetype.EntityCount);
        var citizensWithoutWorkerOrStudentRole = citizenArchetypes
            .Where(archetype => !archetype.HasWorker && !archetype.HasStudent)
            .Sum(archetype => archetype.EntityCount);

        var totalHouseholds = householdArchetypes.Sum(archetype => archetype.EntityCount);
        var commuterHouseholds = householdArchetypes
            .Where(archetype => archetype.IsCommuterHousehold)
            .Sum(archetype => archetype.EntityCount);
        var touristHouseholds = householdArchetypes
            .Where(archetype => archetype.IsTouristHousehold)
            .Sum(archetype => archetype.EntityCount);
        var homelessHouseholds = householdArchetypes
            .Where(archetype => archetype.IsHomelessHousehold)
            .Sum(archetype => archetype.EntityCount);

        return new PopulationDomainFacts(
            new CitizenRoleSummaryFact(totalCitizens, householdMembers, workerCitizens, studentCitizens),
            new HouseholdRoleSummaryFact(totalHouseholds, commuterHouseholds, touristHouseholds, homelessHouseholds),
            new LaborMarketSummaryFact(workerCitizens, studentCitizens, workerStudents, citizensWithoutWorkerOrStudentRole),
            new ReadOnlyCollection<CitizenArchetypePopulationFact>(
                citizenArchetypes
                    .OrderBy(archetype => archetype.ArchetypeIndex)
                    .ToList()),
            new ReadOnlyCollection<HouseholdArchetypePopulationFact>(
                householdArchetypes
                    .OrderBy(archetype => archetype.ArchetypeIndex)
                    .ToList()));
    }
}

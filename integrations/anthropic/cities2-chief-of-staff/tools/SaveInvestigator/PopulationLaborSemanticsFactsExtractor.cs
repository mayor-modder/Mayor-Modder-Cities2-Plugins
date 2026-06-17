using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class PopulationLaborSemanticsFactsExtractor
{
    public static PopulationLaborSemanticsFacts Extract(
        PopulationDomainFacts populationDomainFacts,
        PopulationLaborCarrierAuditFacts carrierAuditFacts)
    {
        var groups = new List<PopulationLaborSemanticGroupFact>
        {
            BuildCitizenRoleStructure(populationDomainFacts, carrierAuditFacts),
            BuildHouseholdRoleStructure(populationDomainFacts, carrierAuditFacts),
            BuildHouseholdSpecialCases(populationDomainFacts),
            BuildLaborOverlap(populationDomainFacts),
            BuildTravelActivityContext(populationDomainFacts, carrierAuditFacts)
        };

        var blockers = new List<string>
        {
            "household occupancy semantics remain unresolved",
            "citizen life-stage semantics remain unresolved"
        };

        if (!HasCarrier(carrierAuditFacts, "education_pipeline", "promising", "confirmed_structural"))
        {
            blockers.Add("education pipeline semantics remain unresolved");
        }

        if (!HasCarrier(carrierAuditFacts, "job_pressure", "promising", "confirmed_structural"))
        {
            blockers.Add("job-pressure semantics remain unresolved");
        }

        return new PopulationLaborSemanticsFacts(
            new ReadOnlyCollection<PopulationLaborSemanticGroupFact>(groups),
            new ReadOnlyCollection<string>(blockers.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList()));
    }

    private static PopulationLaborSemanticGroupFact BuildCitizenRoleStructure(
        PopulationDomainFacts populationDomainFacts,
        PopulationLaborCarrierAuditFacts carrierAuditFacts)
    {
        var supportStatus = HasCarrier(carrierAuditFacts, "citizen_role", "confirmed_structural")
            ? "confirmed"
            : "unresolved";
        return new PopulationLaborSemanticGroupFact(
            "citizen_role_structure",
            supportStatus,
            $"Citizen role structure distinguishes {populationDomainFacts.CitizenSummary.Workers} workers, {populationDomainFacts.CitizenSummary.Students} students, and {populationDomainFacts.CitizenSummary.HouseholdMembers} household members across {populationDomainFacts.CitizenSummary.TotalCitizens} citizens.",
            new ReadOnlyCollection<string>(
                [
                    $"coverage:total_citizens={populationDomainFacts.CitizenSummary.TotalCitizens}",
                    $"coverage:workers={populationDomainFacts.CitizenSummary.Workers}",
                    $"coverage:students={populationDomainFacts.CitizenSummary.Students}",
                    $"coverage:household_members={populationDomainFacts.CitizenSummary.HouseholdMembers}"
                ]));
    }

    private static PopulationLaborSemanticGroupFact BuildHouseholdRoleStructure(
        PopulationDomainFacts populationDomainFacts,
        PopulationLaborCarrierAuditFacts carrierAuditFacts)
    {
        var supportStatus = HasCarrier(carrierAuditFacts, "household", "confirmed_structural")
            ? "confirmed"
            : "unresolved";
        return new PopulationLaborSemanticGroupFact(
            "household_role_structure",
            supportStatus,
            $"Household structure distinguishes {populationDomainFacts.HouseholdSummary.CommuterHouseholds} commuter, {populationDomainFacts.HouseholdSummary.TouristHouseholds} tourist, and {populationDomainFacts.HouseholdSummary.HomelessHouseholds} homeless households across {populationDomainFacts.HouseholdSummary.TotalHouseholds} households.",
            new ReadOnlyCollection<string>(
                [
                    $"coverage:total_households={populationDomainFacts.HouseholdSummary.TotalHouseholds}",
                    $"coverage:commuter_households={populationDomainFacts.HouseholdSummary.CommuterHouseholds}",
                    $"coverage:tourist_households={populationDomainFacts.HouseholdSummary.TouristHouseholds}",
                    $"coverage:homeless_households={populationDomainFacts.HouseholdSummary.HomelessHouseholds}"
                ]));
    }

    private static PopulationLaborSemanticGroupFact BuildHouseholdSpecialCases(PopulationDomainFacts populationDomainFacts)
    {
        var specialCaseHouseholds =
            populationDomainFacts.HouseholdSummary.CommuterHouseholds +
            populationDomainFacts.HouseholdSummary.TouristHouseholds +
            populationDomainFacts.HouseholdSummary.HomelessHouseholds;
        var supportStatus = specialCaseHouseholds > 0
            ? "confirmed"
            : "unresolved";
        return new PopulationLaborSemanticGroupFact(
            "household_special_cases",
            supportStatus,
            $"Special household cases cover {specialCaseHouseholds} persisted households across commuter, tourist, and homeless families.",
            new ReadOnlyCollection<string>(
                [
                    $"coverage:special_case_households={specialCaseHouseholds}"
                ]));
    }

    private static PopulationLaborSemanticGroupFact BuildLaborOverlap(PopulationDomainFacts populationDomainFacts)
    {
        var supportStatus = populationDomainFacts.LaborSummary.WorkerStudents > 0
            ? "confirmed"
            : "partial";
        return new PopulationLaborSemanticGroupFact(
            "labor_overlap",
            supportStatus,
            $"Labor overlap distinguishes {populationDomainFacts.LaborSummary.WorkerStudents} worker-students and {populationDomainFacts.LaborSummary.CitizensWithoutWorkerOrStudentRole} citizens without worker/student roles.",
            new ReadOnlyCollection<string>(
                [
                    $"coverage:worker_students={populationDomainFacts.LaborSummary.WorkerStudents}",
                    $"coverage:citizens_without_worker_or_student_role={populationDomainFacts.LaborSummary.CitizensWithoutWorkerOrStudentRole}"
                ]));
    }

    private static PopulationLaborSemanticGroupFact BuildTravelActivityContext(
        PopulationDomainFacts populationDomainFacts,
        PopulationLaborCarrierAuditFacts carrierAuditFacts)
    {
        var citizenArchetypesWithActivityContext = populationDomainFacts.CitizenArchetypes.Count(
            archetype => archetype.HasCurrentBuilding || archetype.HasCurrentTransport || archetype.HasTravelPurpose);
        var supportStatus = citizenArchetypesWithActivityContext > 0 &&
                            HasCarrier(carrierAuditFacts, "labor_market", "promising", "confirmed_structural")
            ? "promising"
            : "unresolved";
        return new PopulationLaborSemanticGroupFact(
            "travel_activity_context",
            supportStatus,
            $"Travel/activity context appears on {citizenArchetypesWithActivityContext} citizen archetypes, but the semantics remain indirect.",
            new ReadOnlyCollection<string>(
                [
                    $"coverage:activity_context_archetypes={citizenArchetypesWithActivityContext}"
                ]));
    }

    private static bool HasCarrier(
        PopulationLaborCarrierAuditFacts carrierAuditFacts,
        string dimension,
        params string[] allowedStatuses)
    {
        return carrierAuditFacts.Dimensions.Any(
            fact =>
                string.Equals(fact.Dimension, dimension, StringComparison.Ordinal) &&
                allowedStatuses.Contains(fact.SupportStatus, StringComparer.Ordinal));
    }
}

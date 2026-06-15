using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class PopulationLaborCarrierAuditFactsExtractor
{
    public static PopulationLaborCarrierAuditFacts Extract(
        SavePreludeSummary summary,
        PopulationDomainFacts populationDomainFacts,
        EconomyDomainFacts economyDomainFacts)
    {
        var componentTypeNames = summary.ComponentTypes
            .Select(component => component.TypeName)
            .ToList();

        var dimensions = new List<PopulationLaborCarrierDimensionFact>
        {
            BuildCitizenRoleDimension(componentTypeNames, populationDomainFacts),
            BuildHouseholdDimension(componentTypeNames, populationDomainFacts),
            BuildLaborMarketDimension(componentTypeNames, populationDomainFacts, economyDomainFacts),
            BuildEducationPipelineDimension(componentTypeNames, populationDomainFacts),
            BuildJobPressureDimension(componentTypeNames, economyDomainFacts)
        };

        return new PopulationLaborCarrierAuditFacts(
            new ReadOnlyCollection<PopulationLaborCarrierDimensionFact>(dimensions));
    }

    private static PopulationLaborCarrierDimensionFact BuildCitizenRoleDimension(
        IReadOnlyCollection<string> componentTypeNames,
        PopulationDomainFacts populationDomainFacts)
    {
        var matchingTypes = CountMatchingTypes(
            componentTypeNames,
            "Game.Citizens.Citizen",
            "Game.Citizens.HouseholdMember",
            "Game.Citizens.Worker",
            "Game.Citizens.Student");
        var supportStatus = populationDomainFacts.CitizenSummary.TotalCitizens > 0 &&
                            matchingTypes >= 3
            ? "confirmed_structural"
            : "unresolved";

        return new PopulationLaborCarrierDimensionFact(
            "citizen_role",
            supportStatus,
            matchingTypes,
            $"Citizen role carriers match {matchingTypes} relevant component families across {populationDomainFacts.CitizenSummary.TotalCitizens} persisted citizens.",
            new ReadOnlyCollection<string>(
                [
                    $"coverage:total_citizens={populationDomainFacts.CitizenSummary.TotalCitizens}",
                    $"coverage:workers={populationDomainFacts.CitizenSummary.Workers}",
                    $"coverage:students={populationDomainFacts.CitizenSummary.Students}"
                ]));
    }

    private static PopulationLaborCarrierDimensionFact BuildHouseholdDimension(
        IReadOnlyCollection<string> componentTypeNames,
        PopulationDomainFacts populationDomainFacts)
    {
        var matchingTypes = CountMatchingTypes(
            componentTypeNames,
            "Game.Citizens.Household",
            "Game.Citizens.CommuterHousehold",
            "Game.Citizens.TouristHousehold",
            "Game.Citizens.HomelessHousehold");
        var supportStatus = populationDomainFacts.HouseholdSummary.TotalHouseholds > 0 &&
                            matchingTypes >= 2
            ? "confirmed_structural"
            : "unresolved";

        return new PopulationLaborCarrierDimensionFact(
            "household",
            supportStatus,
            matchingTypes,
            $"Household carriers match {matchingTypes} relevant component families across {populationDomainFacts.HouseholdSummary.TotalHouseholds} persisted households.",
            new ReadOnlyCollection<string>(
                [
                    $"coverage:total_households={populationDomainFacts.HouseholdSummary.TotalHouseholds}",
                    $"coverage:commuter_households={populationDomainFacts.HouseholdSummary.CommuterHouseholds}",
                    $"coverage:tourist_households={populationDomainFacts.HouseholdSummary.TouristHouseholds}",
                    $"coverage:homeless_households={populationDomainFacts.HouseholdSummary.HomelessHouseholds}"
                ]));
    }

    private static PopulationLaborCarrierDimensionFact BuildLaborMarketDimension(
        IReadOnlyCollection<string> componentTypeNames,
        PopulationDomainFacts populationDomainFacts,
        EconomyDomainFacts economyDomainFacts)
    {
        var matchingTypes = CountMatchingTypes(
            componentTypeNames,
            "Game.Citizens.Worker",
            "Game.Citizens.Student",
            "Game.Citizens.CurrentTransport",
            "Game.Citizens.TravelPurpose",
            "Game.Companies.WorkProvider");
        var supportStatus = populationDomainFacts.LaborSummary.WorkerCitizens > 0 &&
                            economyDomainFacts.Summary.WorkProviders > 0
            ? "promising"
            : "unresolved";

        return new PopulationLaborCarrierDimensionFact(
            "labor_market",
            supportStatus,
            matchingTypes,
            $"Labor-market carriers match {matchingTypes} relevant component families, with {populationDomainFacts.LaborSummary.WorkerCitizens} workers and {economyDomainFacts.Summary.WorkProviders} work providers visible in the save.",
            new ReadOnlyCollection<string>(
                [
                    $"coverage:workers={populationDomainFacts.LaborSummary.WorkerCitizens}",
                    $"coverage:students={populationDomainFacts.LaborSummary.StudentCitizens}",
                    $"coverage:work_providers={economyDomainFacts.Summary.WorkProviders}"
                ]));
    }

    private static PopulationLaborCarrierDimensionFact BuildEducationPipelineDimension(
        IReadOnlyCollection<string> componentTypeNames,
        PopulationDomainFacts populationDomainFacts)
    {
        var matchingTypes = CountMatchingTypes(
            componentTypeNames,
            "Game.Citizens.Student",
            "Game.Citizens.SchoolSeeker",
            "Game.Citizens.SchoolLevel",
            "Game.Citizens.EducationLevel");
        var supportStatus = matchingTypes >= 2 && populationDomainFacts.LaborSummary.StudentCitizens > 0
            ? "promising"
            : "unresolved";

        return new PopulationLaborCarrierDimensionFact(
            "education_pipeline",
            supportStatus,
            matchingTypes,
            matchingTypes == 0
                ? "No direct education-pipeline carriers are currently confirmed beyond broad student counts."
                : $"Education-pipeline carriers match {matchingTypes} relevant component families, but persisted semantics remain limited.",
            new ReadOnlyCollection<string>(
                [
                    $"coverage:students={populationDomainFacts.LaborSummary.StudentCitizens}"
                ]));
    }

    private static PopulationLaborCarrierDimensionFact BuildJobPressureDimension(
        IReadOnlyCollection<string> componentTypeNames,
        EconomyDomainFacts economyDomainFacts)
    {
        var matchingTypes = CountMatchingTypes(
            componentTypeNames,
            "Game.Companies.WorkProvider",
            "Game.Companies.Employee",
            "Game.Companies.CompanyData",
            "Game.Companies.EmploymentData");
        var supportStatus = economyDomainFacts.Summary.WorkProviders > 0
            ? "promising"
            : "unresolved";

        return new PopulationLaborCarrierDimensionFact(
            "job_pressure",
            supportStatus,
            matchingTypes,
            $"Job-pressure carriers match {matchingTypes} relevant component families, but employer-side pressure semantics are not yet decoded.",
            new ReadOnlyCollection<string>(
                [
                    $"coverage:work_providers={economyDomainFacts.Summary.WorkProviders}",
                    $"coverage:employees={economyDomainFacts.Summary.Employees}"
                ]));
    }

    private static int CountMatchingTypes(IReadOnlyCollection<string> componentTypeNames, params string[] candidateTypeNames)
    {
        return candidateTypeNames.Count(
            candidateTypeName => componentTypeNames.Any(typeName => SerializedTypeMatcher.Matches(typeName, candidateTypeName)));
    }
}

using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class CityUnderstandingFactsExtractor
{
    public static CityUnderstandingFacts Extract(
        TransportDomainFacts transportDomainFacts,
        CityIdentityFacts cityIdentityFacts,
        CityContextPromotionFacts cityContextPromotionFacts,
        PopulationDomainFacts populationDomainFacts,
        EconomyDomainFacts economyDomainFacts,
        ExternalConnectionsFacts externalConnectionsFacts,
        SaveCoverageFacts saveCoverageFacts)
    {
        return Extract(
            transportDomainFacts,
            cityIdentityFacts,
            cityContextPromotionFacts,
            populationDomainFacts,
            new PopulationLaborSemanticsFacts(
                new ReadOnlyCollection<PopulationLaborSemanticGroupFact>([]),
                new ReadOnlyCollection<string>([])),
            LaborDomainFactsExtractor.Extract(populationDomainFacts),
            economyDomainFacts,
            externalConnectionsFacts,
            saveCoverageFacts);
    }

    public static CityUnderstandingFacts Extract(
        TransportDomainFacts transportDomainFacts,
        CityIdentityFacts cityIdentityFacts,
        CityContextPromotionFacts cityContextPromotionFacts,
        PopulationDomainFacts populationDomainFacts,
        PopulationLaborSemanticsFacts populationLaborSemanticsFacts,
        LaborDomainFacts laborDomainFacts,
        EconomyDomainFacts economyDomainFacts,
        ExternalConnectionsFacts externalConnectionsFacts,
        SaveCoverageFacts saveCoverageFacts)
    {
        var domains = new List<CityUnderstandingDomainFact>
        {
            BuildTransportDomain(transportDomainFacts, saveCoverageFacts),
            BuildHousingPopulationDomain(cityIdentityFacts, cityContextPromotionFacts, populationDomainFacts, populationLaborSemanticsFacts),
            BuildLaborWorkforceDomain(populationDomainFacts, populationLaborSemanticsFacts, laborDomainFacts),
            BuildEconomyCompaniesDomain(cityIdentityFacts, cityContextPromotionFacts, economyDomainFacts, laborDomainFacts),
            BuildExternalConnectionsFreightDomain(externalConnectionsFacts)
        };

        var estimatedCompletionPercent = domains.Count == 0
            ? 0
            : (int)Math.Round(domains.Average(domain => domain.ActionabilityPercent), MidpointRounding.AwayFromZero);
        var blockers = domains
            .SelectMany(domain => domain.RemainingBlockers)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(blocker => blocker, StringComparer.Ordinal)
            .ToList();

        return new CityUnderstandingFacts(
            estimatedCompletionPercent,
            new ReadOnlyCollection<CityUnderstandingDomainFact>(domains),
            new ReadOnlyCollection<string>(blockers));
    }

    public static CityUnderstandingFacts Extract(
        TransportDomainFacts transportDomainFacts,
        BuildingDomainFacts buildingDomainFacts,
        PopulationDomainFacts populationDomainFacts,
        EconomyDomainFacts economyDomainFacts,
        ExternalConnectionsFacts externalConnectionsFacts,
        SaveCoverageFacts saveCoverageFacts)
    {
        var cityIdentityFacts = new CityIdentityFacts(
            new ReadOnlyCollection<CityIdentityEntityFact>(
                buildingDomainFacts.Buildings
                    .Select(
                        building => new CityIdentityEntityFact(
                            building.EntityIndex,
                            building.BuildingOwnerChainEntityIndexes.Count > 0
                                ? building.BuildingOwnerChainEntityIndexes[^1]
                                : building.EntityIndex,
                            "generic_building",
                            "base",
                            building.PrefabRefValue,
                            building.CustomName,
                            new ReadOnlyCollection<string>([]),
                            new ReadOnlyCollection<string>([]),
                            new ReadOnlyCollection<string>([])))
                    .ToList()));
        var cityContextPromotionFacts = new CityContextPromotionFacts(
            new ReadOnlyCollection<CityContextPromotionDimensionFact>([]),
            new ReadOnlyCollection<CityContextPromotionEntityFact>([]));

        return Extract(
            transportDomainFacts,
            cityIdentityFacts,
            cityContextPromotionFacts,
            populationDomainFacts,
            new PopulationLaborSemanticsFacts(
                new ReadOnlyCollection<PopulationLaborSemanticGroupFact>([]),
                new ReadOnlyCollection<string>([])),
            LaborDomainFactsExtractor.Extract(populationDomainFacts),
            economyDomainFacts,
            externalConnectionsFacts,
            saveCoverageFacts);
    }

    private static CityUnderstandingDomainFact BuildTransportDomain(
        TransportDomainFacts transportDomainFacts,
        SaveCoverageFacts saveCoverageFacts)
    {
        var totalLines = transportDomainFacts.TransportLines.Count;
        var resolvedLines = transportDomainFacts.TransportLines.Count(line => !string.Equals(line.Mode, "unresolved", StringComparison.Ordinal));
        var resolvedFacilities = transportDomainFacts.TransportFacilities?.Count(facility => string.Equals(facility.ClassificationStatus, "resolved", StringComparison.Ordinal)) ?? 0;
        var totalFacilities = transportDomainFacts.TransportFacilities?.Count ?? 0;
        var resolvedServiceJoins = transportDomainFacts.RailTransitStations.Count(station => string.Equals(station.ServiceJoinStatus, "resolved", StringComparison.Ordinal));
        var totalServiceJoins = transportDomainFacts.RailTransitStations.Count;

        var coveragePercent = AveragePercent(
            CalculatePercent(resolvedLines, totalLines),
            CalculatePercent(resolvedFacilities, totalFacilities),
            CalculatePercent(transportDomainFacts.WaitingPassengers.Stops.Count > 0 ? 1 : 0, 1));
        var reliabilityPercent = AveragePercent(
            CalculatePercent(resolvedLines, totalLines),
            CalculatePercent(resolvedServiceJoins, totalServiceJoins),
            CalculatePercent(resolvedFacilities, totalFacilities));
        var actionabilityPercent = AveragePercent(coveragePercent, reliabilityPercent);
        var blockers = new List<string>();

        if (resolvedLines < totalLines)
        {
            blockers.Add($"mode identity unresolved for {totalLines - resolvedLines} transport lines");
        }

        if (resolvedFacilities < totalFacilities)
        {
            blockers.Add($"facility classification unresolved for {totalFacilities - resolvedFacilities} transport-facing groups");
        }

        blockers.AddRange(
            saveCoverageFacts.OpenItems
                .Where(item => string.Equals(item.Scope, "transport", StringComparison.Ordinal))
                .Select(item => item.Description));

        return BuildDomain(
            "transport",
            coveragePercent,
            reliabilityPercent,
            actionabilityPercent,
            $"Transport understanding covers {resolvedLines} of {totalLines} exact line identities, {resolvedFacilities} of {totalFacilities} facility classifications, and {resolvedServiceJoins} of {totalServiceJoins} resolved station joins.",
            [
                $"coverage:lines={resolvedLines}/{totalLines}",
                $"coverage:facilities={resolvedFacilities}/{totalFacilities}",
                $"coverage:station_joins={resolvedServiceJoins}/{totalServiceJoins}",
                $"coverage:waiting_stops={transportDomainFacts.WaitingPassengers.Stops.Count}"
            ],
            blockers);
    }

    private static CityUnderstandingDomainFact BuildHousingPopulationDomain(
        CityIdentityFacts cityIdentityFacts,
        CityContextPromotionFacts cityContextPromotionFacts,
        PopulationDomainFacts populationDomainFacts,
        PopulationLaborSemanticsFacts populationLaborSemanticsFacts)
    {
        var namedBuildings = cityIdentityFacts.Entities.Count(building => !string.IsNullOrWhiteSpace(building.DisplayName));
        var totalBuildings = cityIdentityFacts.Entities.Count;
        var households = populationDomainFacts.HouseholdSummary.TotalHouseholds;
        var residentialBuildingCandidates = cityIdentityFacts.Entities.Count(
            entity => string.Equals(entity.Family, "residential_building", StringComparison.Ordinal));
        var districtContext = GetContextDimension(cityContextPromotionFacts, "district");
        var networkContext = GetContextDimension(cityContextPromotionFacts, "network");
        var unresolvedContext = cityContextPromotionFacts.Dimensions
            .Where(dimension => string.Equals(dimension.SupportStatus, "unresolved", StringComparison.Ordinal))
            .Select(dimension => dimension.Dimension)
            .OrderBy(dimension => dimension, StringComparer.Ordinal)
            .ToList();
        var householdRoleStructure = populationLaborSemanticsFacts.Groups.FirstOrDefault(
            group => string.Equals(group.GroupKey, "household_role_structure", StringComparison.Ordinal));
        var householdSpecialCases = populationLaborSemanticsFacts.Groups.FirstOrDefault(
            group => string.Equals(group.GroupKey, "household_special_cases", StringComparison.Ordinal));
        var coveragePercent = AveragePercent(
            CalculatePercent(totalBuildings > 0 ? 1 : 0, 1),
            CalculatePercent(households > 0 ? 1 : 0, 1),
            CalculatePercent(namedBuildings, totalBuildings),
            CalculatePercent(residentialBuildingCandidates, totalBuildings),
            CalculatePercent(districtContext?.EntityCount ?? 0, totalBuildings),
            CalculatePercent(string.Equals(householdRoleStructure?.SupportStatus, "confirmed", StringComparison.Ordinal) ? 1 : 0, 1));
        var reliabilityPercent = AveragePercent(
            CalculatePercent(totalBuildings > 0 ? 1 : 0, 1),
            CalculatePercent(households > 0 ? 1 : 0, 1),
            CalculatePercent(districtContext?.EntityCount ?? 0, totalBuildings),
            CalculatePercent(networkContext?.EntityCount ?? 0, totalBuildings));
        var actionabilityPercent = AveragePercent(coveragePercent, reliabilityPercent - 20);
        var blockers = new List<string>();

        if (totalBuildings == 0)
        {
            blockers.Add("building identity coverage is too thin to describe housing pressure");
        }
        else
        {
            blockers.Add("residential occupancy and address-level housing pressure remain unresolved");
        }

        foreach (var unresolvedDimension in unresolvedContext)
        {
            blockers.Add($"{unresolvedDimension.Replace('_', '-')}-level housing context remains unresolved");
        }

        return BuildDomain(
            "housing_population",
            coveragePercent,
            reliabilityPercent,
            actionabilityPercent,
            $"Housing/population facts currently cover {households} households across {totalBuildings} decoded building records, including {residentialBuildingCandidates} residential candidates. Structural district context is proven for {districtContext?.EntityCount ?? 0} buildings and network context for {networkContext?.EntityCount ?? 0}. Household structure is {householdRoleStructure?.SupportStatus ?? "unresolved"}, and special-case households are {householdSpecialCases?.SupportStatus ?? "unresolved"}.",
            [
                $"coverage:households={households}",
                $"coverage:buildings={totalBuildings}",
                $"coverage:named_buildings={namedBuildings}",
                $"coverage:residential_buildings={residentialBuildingCandidates}",
                $"coverage:district_context={districtContext?.EntityCount ?? 0}/{totalBuildings}",
                $"coverage:network_context={networkContext?.EntityCount ?? 0}/{totalBuildings}",
                $"semantics:household_role_structure={householdRoleStructure?.SupportStatus ?? "unresolved"}",
                $"semantics:household_special_cases={householdSpecialCases?.SupportStatus ?? "unresolved"}"
            ],
            blockers);
    }

    private static CityUnderstandingDomainFact BuildLaborWorkforceDomain(
        PopulationDomainFacts populationDomainFacts,
        PopulationLaborSemanticsFacts populationLaborSemanticsFacts,
        LaborDomainFacts laborDomainFacts)
    {
        var workers = populationDomainFacts.LaborSummary.WorkerCitizens;
        var students = populationDomainFacts.LaborSummary.StudentCitizens;
        var workerStudents = populationDomainFacts.LaborSummary.WorkerStudents;
        var laborOverlap = populationLaborSemanticsFacts.Groups.FirstOrDefault(
            group => string.Equals(group.GroupKey, "labor_overlap", StringComparison.Ordinal));
        var travelActivity = populationLaborSemanticsFacts.Groups.FirstOrDefault(
            group => string.Equals(group.GroupKey, "travel_activity_context", StringComparison.Ordinal));
        var coveragePercent = AveragePercent(
            CalculatePercent(workers > 0 ? 1 : 0, 1),
            CalculatePercent(students >= 0 ? 1 : 0, 1),
            CalculatePercent(populationDomainFacts.CitizenSummary.TotalCitizens > 0 ? 1 : 0, 1),
            CalculatePercent(string.Equals(laborOverlap?.SupportStatus, "confirmed", StringComparison.Ordinal) ? 1 : 0, 1));
        var reliabilityPercent = coveragePercent;
        var actionabilityPercent = AveragePercent(coveragePercent, 40);

        return BuildDomain(
            "labor_workforce",
            coveragePercent,
            reliabilityPercent,
            actionabilityPercent,
            $"Labor/workforce facts cover {workers} workers, {students} students, and {workerStudents} worker-students. Labor overlap is {laborOverlap?.SupportStatus ?? "unresolved"} and travel/activity context is {travelActivity?.SupportStatus ?? "unresolved"}, but employer-job-match causality is still unresolved.",
            [
                $"coverage:workers={workers}",
                $"coverage:students={students}",
                $"coverage:worker_students={workerStudents}",
                $"semantics:labor_overlap={laborOverlap?.SupportStatus ?? "unresolved"}",
                $"semantics:travel_activity_context={travelActivity?.SupportStatus ?? "unresolved"}"
            ],
            laborDomainFacts.RemainingBlockers);
    }

    private static CityUnderstandingDomainFact BuildEconomyCompaniesDomain(
        CityIdentityFacts cityIdentityFacts,
        CityContextPromotionFacts cityContextPromotionFacts,
        EconomyDomainFacts economyDomainFacts,
        LaborDomainFacts laborDomainFacts)
    {
        var summary = economyDomainFacts.Summary;
        var totalBuildings = cityIdentityFacts.Entities.Count;
        var officeBuildings = cityIdentityFacts.Entities.Count(entity => string.Equals(entity.Family, "office_building", StringComparison.Ordinal));
        var commercialBuildings = cityIdentityFacts.Entities.Count(entity => string.Equals(entity.Family, "commercial_building", StringComparison.Ordinal));
        var industrialBuildings = cityIdentityFacts.Entities.Count(entity => string.Equals(entity.Family, "industrial_building", StringComparison.Ordinal));
        var contextualEconomyBuildings = officeBuildings + commercialBuildings + industrialBuildings;
        var districtContext = GetContextDimension(cityContextPromotionFacts, "district");
        var networkContext = GetContextDimension(cityContextPromotionFacts, "network");
        var unresolvedContext = cityContextPromotionFacts.Dimensions
            .Where(dimension => string.Equals(dimension.SupportStatus, "unresolved", StringComparison.Ordinal))
            .Select(dimension => dimension.Dimension)
            .OrderBy(dimension => dimension, StringComparer.Ordinal)
            .ToList();
        var coveragePercent = AveragePercent(
            CalculatePercent(summary.CompanyEntities > 0 ? 1 : 0, 1),
            CalculatePercent(economyDomainFacts.CompanyArchetypes.Count > 0 ? 1 : 0, 1),
            CalculatePercent(contextualEconomyBuildings, totalBuildings),
            CalculatePercent(districtContext?.EntityCount ?? 0, totalBuildings),
            CalculatePercent(networkContext?.EntityCount ?? 0, totalBuildings));
        var reliabilityPercent = AveragePercent(
            CalculatePercent(summary.CompanyEntities > 0 ? 1 : 0, 1),
            CalculatePercent(economyDomainFacts.CompanyArchetypes.Count > 0 ? 1 : 0, 1),
            CalculatePercent(contextualEconomyBuildings, totalBuildings));
        var actionabilityPercent = AveragePercent(coveragePercent, reliabilityPercent - 25);
        var blockers = new List<string> { "profitability, sector health, and business stress semantics remain unresolved" };
        var jobPressureStatus = laborDomainFacts.EvidenceNotes.FirstOrDefault(
            note => note.StartsWith("carrier:job_pressure=", StringComparison.Ordinal))?.Split('=')[1] ?? "unresolved";

        foreach (var unresolvedDimension in unresolvedContext)
        {
            blockers.Add($"{unresolvedDimension.Replace('_', '-')}-level company facility context remains unresolved");
        }

        return BuildDomain(
            "economy_companies",
            coveragePercent,
            reliabilityPercent,
            actionabilityPercent,
            $"Economy/company facts cover {summary.CompanyEntities} companies, with {officeBuildings} office, {commercialBuildings} commercial, and {industrialBuildings} industrial building candidates. Structural district context is proven for {districtContext?.EntityCount ?? 0} buildings and network context for {networkContext?.EntityCount ?? 0}; labor-side pressure remains {jobPressureStatus}, but business-health semantics remain partial.",
            [
                $"coverage:companies={summary.CompanyEntities}",
                $"coverage:transport_companies={summary.TransportCompanies}",
                $"coverage:resource_entities={summary.ResourceTaggedEntities}",
                $"coverage:office_buildings={officeBuildings}",
                $"coverage:commercial_buildings={commercialBuildings}",
                $"coverage:industrial_buildings={industrialBuildings}",
                $"coverage:district_context={districtContext?.EntityCount ?? 0}/{totalBuildings}",
                $"coverage:network_context={networkContext?.EntityCount ?? 0}/{totalBuildings}",
                laborDomainFacts.EvidenceNotes.FirstOrDefault(note => note.StartsWith("carrier:job_pressure=", StringComparison.Ordinal)) ?? "carrier:job_pressure=unresolved"
            ],
            blockers);
    }

    private static CityUnderstandingDomainFact BuildExternalConnectionsFreightDomain(
        ExternalConnectionsFacts externalConnectionsFacts)
    {
        var transportOutsideConnections = externalConnectionsFacts.TransportOutsideConnections;
        var economyOutsideConnections = externalConnectionsFacts.EconomyOutsideConnections;
        var resolvedFlow = string.Equals(externalConnectionsFacts.FlowStatus, "resolved", StringComparison.Ordinal);
        var coveragePercent = AveragePercent(
            CalculatePercent(transportOutsideConnections > 0 ? 1 : 0, 1),
            CalculatePercent(economyOutsideConnections > 0 ? 1 : 0, 1),
            CalculatePercent(externalConnectionsFacts.CargoTransportLines > 0 ? 1 : 0, 1),
            CalculatePercent(resolvedFlow ? 1 : 0, 1));
        var reliabilityPercent = AveragePercent(
            CalculatePercent(transportOutsideConnections > 0 ? 1 : 0, 1),
            CalculatePercent(economyOutsideConnections > 0 ? 1 : 0, 1),
            CalculatePercent(resolvedFlow ? 1 : 0, 1));
        var actionabilityPercent = AveragePercent(coveragePercent, reliabilityPercent);

        return BuildDomain(
            "external_connections_freight",
            coveragePercent,
            reliabilityPercent,
            actionabilityPercent,
            externalConnectionsFacts.Summary,
            externalConnectionsFacts.EvidenceNotes,
            externalConnectionsFacts.RemainingBlockers);
    }

    private static CityUnderstandingDomainFact BuildDomain(
        string domain,
        int coveragePercent,
        int reliabilityPercent,
        int actionabilityPercent,
        string summary,
        IEnumerable<string> evidenceNotes,
        IEnumerable<string> blockers)
    {
        var normalizedCoverage = ClampPercent(coveragePercent);
        var normalizedReliability = ClampPercent(reliabilityPercent);
        var normalizedActionability = ClampPercent(actionabilityPercent);

        return new CityUnderstandingDomainFact(
            domain,
            ResolveActionabilityStatus(normalizedActionability),
            normalizedCoverage,
            normalizedReliability,
            normalizedActionability,
            summary,
            new ReadOnlyCollection<string>(evidenceNotes.Distinct(StringComparer.Ordinal).ToList()),
            new ReadOnlyCollection<string>(blockers.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList()));
    }

    private static int CalculatePercent(int solved, int total)
    {
        if (total <= 0)
        {
            return 0;
        }

        return ClampPercent((int)Math.Round((solved / (double)total) * 100, MidpointRounding.AwayFromZero));
    }

    private static int AveragePercent(params int[] values)
    {
        if (values.Length == 0)
        {
            return 0;
        }

        return ClampPercent((int)Math.Round(values.Average(), MidpointRounding.AwayFromZero));
    }

    private static int ClampPercent(int value)
    {
        return Math.Max(0, Math.Min(100, value));
    }

    private static string ResolveActionabilityStatus(int actionabilityPercent)
    {
        if (actionabilityPercent >= 90)
        {
            return "sufficient";
        }

        return actionabilityPercent >= 45
            ? "partial"
            : "insufficient";
    }

    private static CityContextPromotionDimensionFact? GetContextDimension(
        CityContextPromotionFacts cityContextPromotionFacts,
        string dimension)
    {
        return cityContextPromotionFacts.Dimensions.FirstOrDefault(
            fact => string.Equals(fact.Dimension, dimension, StringComparison.Ordinal));
    }
}

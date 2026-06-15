using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class CityStateReportFactsExtractor
{
    public static CityStateReportFacts Extract(
        TransportReportFacts transportReportFacts,
        HousingPressureFacts housingPressureFacts,
        LaborDomainFacts laborDomainFacts,
        CompanyHealthFacts companyHealthFacts,
        ExternalConnectionsFacts externalConnectionsFacts,
        CityUnderstandingFacts cityUnderstandingFacts,
        CityContextPromotionFacts cityContextPromotionFacts)
    {
        return Extract(
            transportReportFacts,
            housingPressureFacts,
            laborDomainFacts,
            companyHealthFacts,
            externalConnectionsFacts,
            cityUnderstandingFacts,
            cityContextPromotionFacts,
            new PopulationLaborSemanticsFacts(
                new ReadOnlyCollection<PopulationLaborSemanticGroupFact>([]),
                new ReadOnlyCollection<string>([])));
    }

    public static CityStateReportFacts Extract(
        TransportReportFacts transportReportFacts,
        HousingPressureFacts housingPressureFacts,
        LaborDomainFacts laborDomainFacts,
        CompanyHealthFacts companyHealthFacts,
        ExternalConnectionsFacts externalConnectionsFacts,
        CityUnderstandingFacts cityUnderstandingFacts,
        CityContextPromotionFacts cityContextPromotionFacts,
        PopulationLaborSemanticsFacts populationLaborSemanticsFacts)
    {
        var understandingByDomain = cityUnderstandingFacts.Domains.ToDictionary(domain => domain.Domain, StringComparer.Ordinal);
        var travelActivityStatus = populationLaborSemanticsFacts.Groups.FirstOrDefault(
            group => string.Equals(group.GroupKey, "travel_activity_context", StringComparison.Ordinal))?.SupportStatus ?? "unresolved";

        var sections = new List<CityStateReportSectionFact>
        {
            BuildTransportSection(transportReportFacts, understandingByDomain["transport"]),
            BuildIdentityAwareSection(
                "Housing & Population",
                housingPressureFacts.Summary,
                housingPressureFacts.ActionabilityStatus,
                housingPressureFacts.EvidenceNotes,
                housingPressureFacts.RemainingBlockers,
                understandingByDomain["housing_population"],
                cityContextPromotionFacts,
                populationLaborSemanticsFacts),
            BuildSection(
                "Labor & Workforce",
                $"{laborDomainFacts.Summary} Travel/activity semantics: {travelActivityStatus}.",
                laborDomainFacts.ActionabilityStatus,
                laborDomainFacts.EvidenceNotes,
                laborDomainFacts.RemainingBlockers),
            BuildIdentityAwareSection(
                "Economy & Companies",
                companyHealthFacts.Summary,
                companyHealthFacts.ActionabilityStatus,
                companyHealthFacts.EvidenceNotes,
                companyHealthFacts.RemainingBlockers,
                understandingByDomain["economy_companies"],
                cityContextPromotionFacts,
                populationLaborSemanticsFacts),
            BuildSection("External Connections & Freight", externalConnectionsFacts.Summary, externalConnectionsFacts.ActionabilityStatus, externalConnectionsFacts.EvidenceNotes, externalConnectionsFacts.RemainingBlockers)
        };

        return new CityStateReportFacts(
            cityUnderstandingFacts.EstimatedCompletionPercent,
            new ReadOnlyCollection<CityStateReportSectionFact>(sections));
    }

    public static CityStateReportFacts Extract(
        TransportReportFacts transportReportFacts,
        HousingPressureFacts housingPressureFacts,
        LaborDomainFacts laborDomainFacts,
        CompanyHealthFacts companyHealthFacts,
        ExternalConnectionsFacts externalConnectionsFacts,
        CityUnderstandingFacts cityUnderstandingFacts)
    {
        return Extract(
            transportReportFacts,
            housingPressureFacts,
            laborDomainFacts,
            companyHealthFacts,
            externalConnectionsFacts,
            cityUnderstandingFacts,
            new CityContextPromotionFacts(
                new ReadOnlyCollection<CityContextPromotionDimensionFact>([]),
                new ReadOnlyCollection<CityContextPromotionEntityFact>([])),
            new PopulationLaborSemanticsFacts(
                new ReadOnlyCollection<PopulationLaborSemanticGroupFact>([]),
                new ReadOnlyCollection<string>([])));
    }

    private static CityStateReportSectionFact BuildTransportSection(
        TransportReportFacts transportReportFacts,
        CityUnderstandingDomainFact understanding)
    {
        var topQueue = transportReportFacts.TopQueueHotspots.FirstOrDefault();
        var summary = topQueue is null
            ? understanding.Summary
            : $"{understanding.Summary} Top saved queue hotspot: {topQueue.LineDisplayName} total={topQueue.TotalWaitingPassengers}, max_stop={topQueue.MaxStopQueue}.";

        var evidence = understanding.EvidenceNotes
            .Concat(transportReportFacts.JoinCoverage.Select(coverage => $"join_coverage:{coverage.Mode}:{coverage.CoverageStatus}"))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return BuildSection(
            "Transport",
            summary,
            understanding.ActionabilityStatus,
            new ReadOnlyCollection<string>(evidence),
            understanding.RemainingBlockers);
    }

    private static CityStateReportSectionFact BuildIdentityAwareSection(
        string title,
        string baseSummary,
        string actionabilityStatus,
        ReadOnlyCollection<string> evidenceNotes,
        ReadOnlyCollection<string> blockers,
        CityUnderstandingDomainFact understanding,
        CityContextPromotionFacts cityContextPromotionFacts,
        PopulationLaborSemanticsFacts populationLaborSemanticsFacts)
    {
        var district = cityContextPromotionFacts.Dimensions.FirstOrDefault(
            dimension => string.Equals(dimension.Dimension, "district", StringComparison.Ordinal));
        var network = cityContextPromotionFacts.Dimensions.FirstOrDefault(
            dimension => string.Equals(dimension.Dimension, "network", StringComparison.Ordinal));
        var unresolved = cityContextPromotionFacts.Dimensions
            .Where(dimension => string.Equals(dimension.SupportStatus, "unresolved", StringComparison.Ordinal))
            .Select(dimension => dimension.Dimension.Replace('_', '-'))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        var contextSummaryParts = new List<string>();

        if (district is not null && string.Equals(district.SupportStatus, "proven", StringComparison.Ordinal))
        {
            contextSummaryParts.Add($"district context is proven for {district.EntityCount} buildings");
        }

        if (network is not null && string.Equals(network.SupportStatus, "proven", StringComparison.Ordinal))
        {
            contextSummaryParts.Add($"network context is proven for {network.EntityCount} buildings");
        }

        if (unresolved.Count > 0)
        {
            contextSummaryParts.Add($"remaining context ceilings: {string.Join(", ", unresolved)}");
        }

        var householdRole = populationLaborSemanticsFacts.Groups.FirstOrDefault(
            group => string.Equals(group.GroupKey, "household_role_structure", StringComparison.Ordinal));
        var laborTravel = populationLaborSemanticsFacts.Groups.FirstOrDefault(
            group => string.Equals(group.GroupKey, "travel_activity_context", StringComparison.Ordinal));
        if (householdRole is not null || laborTravel is not null)
        {
            contextSummaryParts.Add($"population/labor semantics: household={householdRole?.SupportStatus ?? "unresolved"}, travel/activity={laborTravel?.SupportStatus ?? "unresolved"}");
        }

        var summary = contextSummaryParts.Count == 0
            ? understanding.Summary
            : $"{understanding.Summary} Structural context gains: {string.Join("; ", contextSummaryParts)}.";
        var mergedEvidence = evidenceNotes
            .Concat(understanding.EvidenceNotes)
            .Concat(cityContextPromotionFacts.Dimensions.Select(dimension => $"context:{dimension.Dimension}:{dimension.SupportStatus}:{dimension.EntityCount}"))
            .Concat(populationLaborSemanticsFacts.Groups.Select(group => $"semantics:{group.GroupKey}:{group.SupportStatus}"))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var mergedBlockers = blockers
            .Concat(understanding.RemainingBlockers)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();

        return BuildSection(
            title,
            summary,
            actionabilityStatus,
            new ReadOnlyCollection<string>(mergedEvidence),
            new ReadOnlyCollection<string>(mergedBlockers));
    }

    private static CityStateReportSectionFact BuildSection(
        string title,
        string summary,
        string actionabilityStatus,
        ReadOnlyCollection<string> evidenceNotes,
        ReadOnlyCollection<string> blockers)
    {
        return new CityStateReportSectionFact(title, summary, actionabilityStatus, evidenceNotes, blockers);
    }
}

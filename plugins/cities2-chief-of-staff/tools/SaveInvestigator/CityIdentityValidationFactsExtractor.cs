using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class CityIdentityValidationFactsExtractor
{
    public static CityIdentityValidationFacts Extract(
        CityIdentityFacts cityIdentityFacts,
        TransportDomainFacts transportDomainFacts)
    {
        var transportFacilities = transportDomainFacts.TransportFacilities ?? new ReadOnlyCollection<TransportFacilityFact>([]);
        var identityByBaseEntityIndex = cityIdentityFacts.Entities
            .GroupBy(entity => entity.BaseEntityIndex)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(entity => entity.EntityIndex == entity.BaseEntityIndex ? 0 : 1)
                    .ThenBy(entity => entity.EntityIndex)
                    .First());

        var cases = transportFacilities
            .Select(
                facility =>
                {
                    identityByBaseEntityIndex.TryGetValue(facility.BaseFacilityEntityIndex, out var identity);
                    var validationKind = string.Equals(facility.ClassificationStatus, "resolved", StringComparison.Ordinal)
                        ? "facility_confirmation"
                        : "facility_identity";
                    var outcomeStatus = ResolveOutcomeStatus(facility, identity);
                    var summary = BuildSummary(facility, identity, outcomeStatus);
                    var evidenceNotes = new List<string>
                    {
                        $"facility_role:{facility.Role}",
                        $"facility_mode:{facility.Mode}",
                        $"facility_status:{facility.ClassificationStatus}"
                    };

                    if (identity is not null)
                    {
                        evidenceNotes.Add($"city_identity_family:{identity.Family}");
                        evidenceNotes.Add($"city_relationship:{identity.RelationshipStatus}");
                    }

                    return new CityIdentityValidationCaseFact(
                        facility.Name,
                        facility.BaseFacilityEntityIndex,
                        validationKind,
                        outcomeStatus,
                        summary,
                        new ReadOnlyCollection<string>(evidenceNotes));
                })
            .OrderBy(caseFact => caseFact.OutcomeStatus, StringComparer.Ordinal)
            .ThenBy(caseFact => caseFact.DisplayName, StringComparer.Ordinal)
            .ToList();

        return new CityIdentityValidationFacts(
            new ReadOnlyCollection<CityIdentityValidationCaseFact>(cases));
    }

    private static string ResolveOutcomeStatus(
        TransportFacilityFact facility,
        CityIdentityEntityFact? identity)
    {
        if (identity is null)
        {
            return "still_blocked";
        }

        if (!string.Equals(facility.ClassificationStatus, "resolved", StringComparison.Ordinal))
        {
            return string.Equals(identity.Family, "transport_building", StringComparison.Ordinal)
                ? "improved"
                : "still_blocked";
        }

        return string.Equals(identity.Family, "transport_building", StringComparison.Ordinal)
            ? "unchanged"
            : "improved";
    }

    private static string BuildSummary(
        TransportFacilityFact facility,
        CityIdentityEntityFact? identity,
        string outcomeStatus)
    {
        if (identity is null)
        {
            return "No matching city-identity record exists for this transport facility yet.";
        }

        return outcomeStatus switch
        {
            "improved" when string.Equals(facility.ClassificationStatus, "resolved", StringComparison.Ordinal) =>
                $"Resolved transport facility classification now has a matched city identity family of {identity.Family}.",
            "improved" =>
                $"City identity narrows this unresolved transport facility to {identity.Family} even though the exact facility role is still not fully proven.",
            "unchanged" =>
                $"Resolved transport facility classification remains consistent with city identity family {identity.Family}.",
            _ =>
                $"City identity still does not sharpen this transport facility beyond {identity.Family}."
        };
    }
}

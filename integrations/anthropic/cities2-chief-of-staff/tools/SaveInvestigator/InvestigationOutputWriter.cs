using System.Text.Json;

namespace SaveInvestigator;

public static class InvestigationOutputWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task WriteAsync(string outputDirectory, InvestigationArtifacts artifacts, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);

        await WriteJsonAsync(Path.Combine(outputDirectory, "save-prelude.json"), artifacts.Prelude, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "save-report.json"), artifacts.Report, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "transport-domain-facts.json"), artifacts.TransportDomainFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "transport-facility-classification-audit-facts.json"), artifacts.TransportFacilityClassificationAuditFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "transport-join-path-audit-facts.json"), artifacts.TransportJoinPathAuditFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "transport-station-service-audit-facts.json"), artifacts.TransportStationServiceAuditFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "transport-service-join-facts.json"), artifacts.TransportServiceJoinFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "transport-report-facts.json"), artifacts.TransportReportFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "transport-readability-gap-facts.json"), artifacts.TransportReadabilityGapFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "transit-understanding-facts.json"), artifacts.TransitUnderstandingFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "city-understanding-facts.json"), artifacts.CityUnderstandingFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "city-state-report-facts.json"), artifacts.CityStateReportFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "building-domain-facts.json"), artifacts.BuildingDomainFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "building-identity-audit-facts.json"), artifacts.BuildingIdentityAuditFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "city-identity-carrier-audit-facts.json"), artifacts.CityIdentityCarrierAuditFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "city-identity-facts.json"), artifacts.CityIdentityFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "city-identity-validation-facts.json"), artifacts.CityIdentityValidationFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "city-context-promotion-facts.json"), artifacts.CityContextPromotionFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "housing-pressure-facts.json"), artifacts.HousingPressureFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "population-labor-carrier-audit-facts.json"), artifacts.PopulationLaborCarrierAuditFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "population-labor-semantics-facts.json"), artifacts.PopulationLaborSemanticsFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "population-domain-facts.json"), artifacts.PopulationDomainFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "labor-domain-facts.json"), artifacts.LaborDomainFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "economy-domain-facts.json"), artifacts.EconomyDomainFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "company-health-facts.json"), artifacts.CompanyHealthFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "external-connections-facts.json"), artifacts.ExternalConnectionsFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "save-coverage-facts.json"), artifacts.SaveCoverageFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "targeted-facts.json"), artifacts.TargetedFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "system-buffer-facts.json"), artifacts.SystemBufferFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "system-buffer-catalog-facts.json"), artifacts.SystemBufferCatalogFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "system-table-facts.json"), artifacts.SystemTableFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "passenger-train-station-facts.json"), artifacts.PassengerTrainStationFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "rail-transit-station-facts.json"), artifacts.RailTransitStationFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "transport-station-graph-facts.json"), artifacts.TransportStationGraphFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "name-and-indirect-resolution-facts.json"), artifacts.NameAndIndirectResolutionFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "entity-graph-facts.json"), artifacts.EntityGraphFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "serializer-catalog-facts.json"), artifacts.SerializerCatalogFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "primitive-component-facts.json"), artifacts.PrimitiveComponentFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "dynamic-buffer-facts.json"), artifacts.DynamicBufferFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "component-block-shape-facts.json"), artifacts.ComponentBlockShapeFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "outer-buffer-string-facts.json"), artifacts.OuterBufferStringFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "entity-backlink-facts.json"), artifacts.EntityBacklinkFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "transport-line-mode-audit-facts.json"), artifacts.TransportLineModeAuditFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "remaining-rail-identity-facts.json"), artifacts.RemainingRailIdentityFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "connected-node-layout-facts.json"), artifacts.ConnectedNodeLayoutFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "rail-track-connectivity-facts.json"), artifacts.RailTrackConnectivityFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "transport-topology-facts.json"), artifacts.TransportTopologyFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "managed-recon-notes-facts.json"), artifacts.ManagedReconNotesFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "prefab-identity-hypothesis-facts.json"), artifacts.PrefabIdentityHypothesisFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "company-resource-hypothesis-facts.json"), artifacts.CompanyResourceHypothesisFacts, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "assembly-inventory.json"), artifacts.AssemblyInventory, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "candidate-types.json"), artifacts.CandidateTypes, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "component-type-resolutions.json"), artifacts.ComponentTypeResolutions, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "system-type-resolutions.json"), artifacts.SystemTypeResolutions, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "artifacts.json"), artifacts, cancellationToken);
    }

    private static async Task WriteJsonAsync(string path, object value, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
    }
}

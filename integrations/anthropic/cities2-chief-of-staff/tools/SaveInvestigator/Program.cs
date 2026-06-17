namespace SaveInvestigator;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            var explicitSavePath = args.Length > 0 ? args[0] : null;
            var managedPath = GamePathLocator.ResolveManagedPath();
            var savePath = GamePathLocator.ResolveSavePath(explicitSavePath);
            var savesRoot = GamePathLocator.ResolveSavesRoot();
            var outputDirectory = ResolveOutputDirectory();

            var container = await SaveContainerReader.ReadAsync(savePath);
            var prelude = SaveGameDataParser.ParsePrelude(container.SaveGameData);
            var report = SaveInvestigationReportBuilder.Build(prelude);
            var systemBufferCatalogFacts = SystemBufferCatalogFactsExtractor.Extract(
                container.SaveGameData,
                prelude);
            var systemTableFacts = SystemTableFactsExtractor.Extract(
                container.SaveGameData,
                prelude,
                systemBufferCatalogFacts);
            var serializerCatalogFacts = SerializerCatalogFactsExtractor.Extract(
                container.SaveGameData,
                prelude,
                managedPath);
            var dynamicBufferFacts = DynamicBufferFactsExtractor.Extract(
                container.SaveGameData,
                prelude,
                serializerCatalogFacts);
            var entityGraphFacts = EntityGraphFactsExtractor.Extract(
                container.SaveGameData,
                prelude,
                serializerCatalogFacts,
                dynamicBufferFacts);
            var transportLineModeAuditFacts = TransportLineModeAuditFactsExtractor.Extract(
                container.SaveGameData,
                prelude,
                entityGraphFacts);
            var buildingDomainFacts = BuildingDomainFactsExtractor.Extract(
                prelude,
                systemTableFacts,
                entityGraphFacts);
            var buildingIdentityAuditFacts = BuildingIdentityAuditFactsExtractor.Extract(
                buildingDomainFacts,
                entityGraphFacts);
            var cityIdentityCarrierAuditFacts = CityIdentityCarrierAuditFactsExtractor.Extract(
                prelude,
                buildingDomainFacts,
                entityGraphFacts);
            var managedReconNotesFacts = ManagedReconNotesFactsExtractor.Extract();
            var prefabIdentityHypothesisFacts = PrefabIdentityHypothesisFactsExtractor.Extract(
                buildingDomainFacts,
                managedReconNotesFacts);
            var cityIdentityFacts = CityIdentityFactsExtractor.Extract(
                buildingDomainFacts,
                entityGraphFacts,
                prefabIdentityHypothesisFacts,
                cityIdentityCarrierAuditFacts);
            var populationDomainFacts = PopulationDomainFactsExtractor.Extract(prelude);
            var economyDomainFacts = EconomyDomainFactsExtractor.Extract(prelude);
            var populationLaborCarrierAuditFacts = PopulationLaborCarrierAuditFactsExtractor.Extract(
                prelude,
                populationDomainFacts,
                economyDomainFacts);
            var populationLaborSemanticsFacts = PopulationLaborSemanticsFactsExtractor.Extract(
                populationDomainFacts,
                populationLaborCarrierAuditFacts);
            var housingPressureFacts = HousingPressureFactsExtractor.Extract(
                cityIdentityFacts,
                populationDomainFacts,
                populationLaborSemanticsFacts);
            var laborDomainFacts = LaborDomainFactsExtractor.Extract(
                populationDomainFacts,
                populationLaborSemanticsFacts,
                populationLaborCarrierAuditFacts);
            var companyHealthFacts = CompanyHealthFactsExtractor.Extract(
                economyDomainFacts,
                cityIdentityFacts,
                laborDomainFacts,
                populationLaborSemanticsFacts);
            var baseTransportDomainFacts = TransportDomainFactsExtractor.Extract(
                container.SaveGameData,
                prelude,
                systemTableFacts,
                entityGraphFacts,
                transportLineModeAuditFacts);
            var transportFacilityClassificationAuditFacts = TransportFacilityClassificationAuditFactsExtractor.Extract(
                buildingDomainFacts,
                baseTransportDomainFacts,
                entityGraphFacts);
            var transportJoinPathAuditFacts = TransportJoinPathAuditFactsExtractor.Extract(
                container.SaveGameData,
                prelude,
                baseTransportDomainFacts);
            var transportStationServiceAuditFacts = TransportStationServiceAuditFactsExtractor.Extract(
                baseTransportDomainFacts,
                systemTableFacts,
                entityGraphFacts);
            var transportDomainFacts = TransportDomainFactsExtractor.ApplyTransportFacilityClassificationAudit(
                baseTransportDomainFacts,
                transportFacilityClassificationAuditFacts);
            transportDomainFacts = TransportDomainFactsExtractor.ApplyStationServiceAudit(
                transportDomainFacts,
                transportStationServiceAuditFacts);
            var remainingRailIdentityFacts = RemainingRailIdentityFactsExtractor.Extract(
                transportDomainFacts,
                transportLineModeAuditFacts,
                transportStationServiceAuditFacts,
                systemTableFacts);
            transportDomainFacts = TransportDomainFactsExtractor.ApplyRemainingRailIdentityAudit(
                transportDomainFacts,
                remainingRailIdentityFacts);
            var transportServiceJoinFacts = TransportServiceJoinFactsExtractor.Extract(
                container.SaveGameData,
                prelude,
                transportDomainFacts,
                systemTableFacts);
            transportDomainFacts = TransportDomainFactsExtractor.ApplyTransportServiceJoinFacts(
                transportDomainFacts,
                transportServiceJoinFacts);
            transportDomainFacts = TransportDomainFactsExtractor.ApplyFacilityBackedLineIdentity(transportDomainFacts);
            var cityIdentityValidationFacts = CityIdentityValidationFactsExtractor.Extract(
                cityIdentityFacts,
                transportDomainFacts);
            var cityContextPromotionFacts = CityContextPromotionFactsExtractor.Extract(
                prelude,
                buildingDomainFacts,
                cityIdentityFacts,
                cityIdentityCarrierAuditFacts);
            var externalConnectionsFacts = ExternalConnectionsFactsExtractor.Extract(
                transportDomainFacts,
                economyDomainFacts);
            var railTransitStationFacts = RailTransitStationFactsExtractor.Extract(transportDomainFacts);
            var connectedNodeLayoutFacts = ConnectedNodeLayoutFactsExtractor.Extract(
                container.SaveGameData,
                prelude);
            var railTrackConnectivityFacts = RailTrackConnectivityFactsExtractor.Extract(
                container.SaveGameData,
                prelude,
                railTransitStationFacts);
            var transportTopologyFacts = TransportTopologyFactsExtractor.Extract(
                transportDomainFacts,
                systemTableFacts,
                railTrackConnectivityFacts);
            var transportReportFacts = TransportReportFactsExtractor.Extract(
                transportDomainFacts,
                systemTableFacts,
                transportTopologyFacts,
                transportServiceJoinFacts);
            var companyResourceHypothesisFacts = CompanyResourceHypothesisFactsExtractor.Extract(
                economyDomainFacts,
                serializerCatalogFacts,
                managedReconNotesFacts);
            var (assemblyInventory, candidateTypes) = AssemblyInventoryBuilder.Build(managedPath);
            var componentTypeResolutions = AssemblyInventoryBuilder.ResolveSerializedTypes(
                managedPath,
                prelude.ComponentTypes.Select(component => component.TypeName));
            var systemTypeResolutions = AssemblyInventoryBuilder.ResolveSerializedTypes(
                managedPath,
                prelude.SystemTypes.Select(system => system.TypeName));
            var saveCoverageFacts = SaveCoverageFactsExtractor.Extract(
                serializerCatalogFacts,
                systemTableFacts,
                componentTypeResolutions,
                systemTypeResolutions);
            var transportReadabilityGapFacts = TransportReadabilityGapFactsExtractor.Extract(
                transportDomainFacts,
                transportReportFacts,
                saveCoverageFacts);
            var transitUnderstandingFacts = TransitUnderstandingFactsExtractor.Extract(
                transportDomainFacts,
                transportReportFacts,
                transportTopologyFacts);
            var cityUnderstandingFacts = CityUnderstandingFactsExtractor.Extract(
                transportDomainFacts,
                cityIdentityFacts,
                cityContextPromotionFacts,
                populationDomainFacts,
                populationLaborSemanticsFacts,
                laborDomainFacts,
                economyDomainFacts,
                externalConnectionsFacts,
                saveCoverageFacts);
            var cityStateReportFacts = CityStateReportFactsExtractor.Extract(
                transportReportFacts,
                housingPressureFacts,
                laborDomainFacts,
                companyHealthFacts,
                externalConnectionsFacts,
                cityUnderstandingFacts,
                cityContextPromotionFacts,
                populationLaborSemanticsFacts);
            var targetedFacts = TargetedSaveFactsExtractor.Extract(transportDomainFacts);
            var systemBufferFacts = SystemBufferFactsExtractor.Extract(
                container.SaveGameData,
                prelude,
                targetedFacts,
                systemTableFacts);
            var passengerTrainStationFacts = PassengerTrainStationFactsExtractor.Extract(
                container.SaveGameData,
                prelude,
                targetedFacts,
                systemBufferFacts);
            var transportStationGraphFacts = TransportStationGraphFactsExtractor.Extract(
                container.SaveGameData,
                prelude,
                systemBufferFacts,
                railTransitStationFacts);
            var nameAndIndirectResolutionFacts = NameAndIndirectResolutionFactsExtractor.Extract(
                systemTableFacts,
                targetedFacts,
                railTransitStationFacts,
                passengerTrainStationFacts);
            var primitiveComponentFacts = PrimitiveComponentFactsExtractor.Extract(
                container.SaveGameData,
                prelude,
                serializerCatalogFacts);
            var componentBlockShapeFacts = ComponentBlockShapeFactsExtractor.Extract(
                container.SaveGameData,
                prelude,
                railTransitStationFacts,
                transportStationGraphFacts);
            var outerBufferStringFacts = OuterBufferStringFactsExtractor.Extract(
                container.SaveGameData,
                prelude,
                railTransitStationFacts,
                transportStationGraphFacts);
            var entityBacklinkFacts = EntityBacklinkFactsExtractor.Extract(
                container.SaveGameData,
                prelude,
                transportStationGraphFacts);
            var artifacts = new InvestigationArtifacts(
                savePath,
                managedPath,
                savesRoot,
                container.Metadata,
                prelude,
                report,
                transportDomainFacts,
                transportFacilityClassificationAuditFacts,
                transportJoinPathAuditFacts,
                transportStationServiceAuditFacts,
                transportServiceJoinFacts,
                transportReportFacts,
                transportReadabilityGapFacts,
                transitUnderstandingFacts,
                cityUnderstandingFacts,
                cityStateReportFacts,
                buildingDomainFacts,
                buildingIdentityAuditFacts,
                cityIdentityCarrierAuditFacts,
                cityIdentityFacts,
                cityIdentityValidationFacts,
                cityContextPromotionFacts,
                housingPressureFacts,
                populationLaborCarrierAuditFacts,
                populationLaborSemanticsFacts,
                populationDomainFacts,
                laborDomainFacts,
                economyDomainFacts,
                companyHealthFacts,
                externalConnectionsFacts,
                saveCoverageFacts,
                targetedFacts,
                systemBufferFacts,
                systemBufferCatalogFacts,
                systemTableFacts,
                passengerTrainStationFacts,
                railTransitStationFacts,
                transportStationGraphFacts,
                nameAndIndirectResolutionFacts,
                entityGraphFacts,
                serializerCatalogFacts,
                primitiveComponentFacts,
                dynamicBufferFacts,
                componentBlockShapeFacts,
                outerBufferStringFacts,
                entityBacklinkFacts,
                transportLineModeAuditFacts,
                remainingRailIdentityFacts,
                connectedNodeLayoutFacts,
                railTrackConnectivityFacts,
                transportTopologyFacts,
                managedReconNotesFacts,
                prefabIdentityHypothesisFacts,
                companyResourceHypothesisFacts,
                assemblyInventory,
                candidateTypes,
                componentTypeResolutions,
                systemTypeResolutions);

            await InvestigationOutputWriter.WriteAsync(outputDirectory, artifacts);
            PrintSummary(artifacts, outputDirectory);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static string ResolveOutputDirectory()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "output");
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        return Path.Combine(root, timestamp);
    }

    private static void PrintSummary(InvestigationArtifacts artifacts, string outputDirectory)
    {
        Console.WriteLine($"Save: {artifacts.SavePath}");
        Console.WriteLine($"Managed: {artifacts.ManagedPath}");
        Console.WriteLine($"Output: {outputDirectory}");

        if (artifacts.Metadata is not null &&
            artifacts.Metadata.TryGetValue("cityName", out var cityName))
        {
            Console.WriteLine($"City: {cityName}");
        }

        Console.WriteLine($"Component types: {artifacts.Prelude.ComponentTypes.Count}");
        Console.WriteLine($"System types: {artifacts.Prelude.SystemTypes.Count}");
        Console.WriteLine($"Archetypes: {artifacts.Prelude.Archetypes.Count}");
        Console.WriteLine($"Total serialized entities: {artifacts.Report.TotalEntityCount}");
        Console.WriteLine("Highlights:");

        foreach (var highlight in artifacts.Report.HighlightMetrics)
        {
            Console.WriteLine($"  {highlight.Key,-20} {highlight.EntityCoverage,8}  {highlight.TypeName}");
        }

        Console.WriteLine("Top component coverage:");

        foreach (var component in artifacts.Report.TopComponentCoverage.Take(10))
        {
            Console.WriteLine($"  {component.EntityCoverage,8}  {component.TypeName}");
        }

        PrintTargetedFacts(artifacts.TargetedFacts);
        foreach (var line in ProgramSummaryBuilder.BuildTransportSummaryLines(artifacts.TransportReportFacts))
        {
            Console.WriteLine(line);
        }

        foreach (var line in ProgramSummaryBuilder.BuildCityStateSummaryLines(artifacts.CityStateReportFacts))
        {
            Console.WriteLine(line);
        }
    }

    private static void PrintTargetedFacts(TargetedSaveFacts targetedFacts)
    {
        Console.WriteLine("Targeted facts:");
        Console.WriteLine($"  transport lines          {targetedFacts.TransportLines.Count}");

        var uniqueRouteNumbers = targetedFacts.TransportLines
            .Select(line => line.RouteNumber)
            .Distinct()
            .OrderBy(number => number)
            .ToArray();
        Console.WriteLine($"  route numbers            {string.Join(", ", uniqueRouteNumbers)}");

        var flaggedLines = targetedFacts.TransportLines
            .Where(line => line.Flags != 0)
            .OrderBy(line => line.RouteNumber)
            .ThenBy(line => line.ColorHex, StringComparer.Ordinal)
            .ToArray();
        if (flaggedLines.Length > 0)
        {
            Console.WriteLine("  flagged lines:");
            foreach (var line in flaggedLines)
            {
                Console.WriteLine(
                    $"    route {line.RouteNumber} {line.ColorHex}  flags={line.Flags}  interval={line.VehicleInterval:F1}s");
            }
        }

        var slowestLines = targetedFacts.TransportLines
            .OrderByDescending(line => line.VehicleInterval)
            .Take(3)
            .ToArray();
        if (slowestLines.Length > 0)
        {
            Console.WriteLine("  slowest line intervals:");
            foreach (var line in slowestLines)
            {
                Console.WriteLine(
                    $"    route {line.RouteNumber} {line.ColorHex}  {line.VehicleInterval:F1}s");
            }
        }

        Console.WriteLine(
            $"  waiting passengers       total={targetedFacts.WaitingPassengers.TotalWaitingPassengers} max_stop={targetedFacts.WaitingPassengers.MaxWaitingPassengers}");

        var topQueues = targetedFacts.WaitingPassengers.Stops
            .OrderByDescending(stop => stop.Count)
            .Take(3)
            .ToArray();
        if (topQueues.Length > 0)
        {
            Console.WriteLine("  busiest stop queues:");
            foreach (var stop in topQueues)
            {
                Console.WriteLine(
                    $"    queue={stop.Count} avg_wait={stop.AverageWaitingTime} stop_ref={stop.ArchetypeIndex}:{stop.EntityOrdinal}");
            }
        }
    }
}

internal static class ProgramSummaryBuilder
{
    internal static IReadOnlyList<string> BuildTransportSummaryLines(TransportReportFacts transportReportFacts)
    {
        var linesByMode = string.Join(
            ", ",
            transportReportFacts.LineGroups
                .OrderBy(group => group.Mode, StringComparer.Ordinal)
                .Select(group => $"{group.Mode}={group.LineCount}"));
        var stationGroups = string.Join(
            ", ",
            transportReportFacts.StationGroups
                .OrderBy(group => group.Mode, StringComparer.Ordinal)
                .Select(group => $"{group.Mode}={group.StationCount}"));
        var topQueue = transportReportFacts.TopQueueHotspots.FirstOrDefault();
        var topQueueStation = topQueue?.TopStop?.StationName is { Length: > 0 } stationName
            ? $" station=\"{stationName}\""
            : string.Empty;
        var topQueueLine = topQueue is null
            ? "  top queue               none"
            : $"  top queue               {topQueue.LineDisplayName} total={topQueue.TotalWaitingPassengers} max_stop={topQueue.MaxStopQueue}{topQueueStation}";
        var namedStations = string.Join(
            "; ",
            transportReportFacts.StationGroups
                .SelectMany(group => group.Stations)
                .Select(station => station.Name)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .Take(3));
        var outsideFacing = string.Join(
            "; ",
            transportReportFacts.StationGroups
                .SelectMany(group => group.Stations)
                .Where(station => station.OutsideDestinationNames?.Count > 0)
                .OrderBy(station => station.Name, StringComparer.Ordinal)
                .Select(
                    station =>
                        $"{station.Name} -> {string.Join(", ", station.OutsideDestinationNames!.OrderBy(value => value, StringComparer.Ordinal))}")
                .Take(3));
        var exactJoinCoverage = string.Join(
            ", ",
            transportReportFacts.JoinCoverage
                .Where(coverage => coverage.Mode is "train" or "subway" or "ferry")
                .OrderBy(coverage => coverage.Mode, StringComparer.Ordinal)
                .Select(
                    coverage =>
                    {
                        if (TryResolveCoverageFraction(coverage.Summary, out var fraction))
                        {
                            return $"{coverage.Mode}={fraction}";
                        }

                        return $"{coverage.Mode}={coverage.CoverageStatus}";
                    }));
        var noStationCoverage = string.Join(
            ", ",
            transportReportFacts.JoinCoverage
                .Where(coverage => coverage.Mode is not ("train" or "subway" or "ferry"))
                .OrderBy(coverage => coverage.Mode, StringComparer.Ordinal)
                .Select(
                    coverage =>
                        $"{coverage.Mode}={(coverage.CoverageStatus == "no_proven_join_path_without_station_inventory" ? $"no exact path ({ResolveCheckedStopCount(coverage.Summary)} stops)" : coverage.CoverageStatus)}"));

        var lines = new List<string>
        {
            "Transport report:",
            $"  lines by mode           {linesByMode}",
            $"  station groups          {stationGroups}",
            topQueueLine,
            $"  named stations          {namedStations}"
        };
        if (!string.IsNullOrWhiteSpace(exactJoinCoverage))
        {
            lines.Add($"  exact join coverage     {exactJoinCoverage}");
        }

        if (!string.IsNullOrWhiteSpace(noStationCoverage))
        {
            lines.Add($"  checked no-station      {noStationCoverage}");
        }

        if (!string.IsNullOrWhiteSpace(outsideFacing))
        {
            lines.Add($"  outside-facing          {outsideFacing}");
        }

        return lines;
    }

    internal static IReadOnlyList<string> BuildCityStateSummaryLines(CityStateReportFacts cityStateReportFacts)
    {
        var lines = new List<string>
        {
            $"City understanding       {cityStateReportFacts.EstimatedCompletionPercent}%"
        };

        foreach (var section in cityStateReportFacts.Sections)
        {
            lines.Add($"  {section.Title,-24} {section.ActionabilityStatus}");
        }

        return lines;
    }

    private static bool TryResolveCoverageFraction(string summary, out string fraction)
    {
        var match = System.Text.RegularExpressions.Regex.Match(summary, "(\\d+) of (\\d+)");
        if (match.Success)
        {
            fraction = $"{match.Groups[1].Value}/{match.Groups[2].Value}";
            return true;
        }

        fraction = string.Empty;
        return false;
    }

    private static string ResolveCheckedStopCount(string summary)
    {
        var match = System.Text.RegularExpressions.Regex.Match(summary, "checked (\\d+) line-owned stops");
        return match.Success
            ? match.Groups[1].Value
            : "?";
    }
}

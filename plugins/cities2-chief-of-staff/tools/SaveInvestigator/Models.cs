using System.Collections.ObjectModel;

namespace SaveInvestigator;

public sealed record SaveContainer(
    string FilePath,
    IReadOnlyDictionary<string, object?>? Metadata,
    byte[] SaveGameData);

public sealed record SerializedVersion(
    byte VersionVersion,
    long PackedVersion,
    int VersionExtra);

public sealed record ComponentTypeSummary(
    int Index,
    byte SerializerType,
    string TypeName);

public sealed record SystemTypeSummary(
    int Index,
    string TypeName);

public sealed record ArchetypeSummary(
    int Index,
    int EntityCount,
    ReadOnlyCollection<int> ComponentTypeIndexes);

public sealed record SavePreludeSummary(
    SerializedVersion Version,
    byte BufferFormat,
    ReadOnlyCollection<string> FormatTags,
    ReadOnlyCollection<ComponentTypeSummary> ComponentTypes,
    ReadOnlyCollection<SystemTypeSummary> SystemTypes,
    ReadOnlyCollection<ArchetypeSummary> Archetypes);

public sealed record ComponentCoverageSummary(
    int ComponentIndex,
    string TypeName,
    int EntityCoverage);

public sealed record HighlightMetric(
    string Key,
    string TypeName,
    int EntityCoverage);

public sealed record ArchetypeReport(
    int Index,
    int EntityCount,
    ReadOnlyCollection<string> ComponentTypes);

public sealed record SaveInvestigationReport(
    int TotalEntityCount,
    ReadOnlyCollection<HighlightMetric> HighlightMetrics,
    ReadOnlyCollection<ComponentCoverageSummary> TopComponentCoverage,
    ReadOnlyCollection<ArchetypeReport> TopArchetypes);

public sealed record AssemblyInventoryItem(
    string Name,
    string Path,
    int CandidateTypeCount);

public sealed record CandidateTypeInventoryItem(
    string AssemblyName,
    string FullName);

public sealed record TypeResolutionSummary(
    string TypeName,
    bool Resolved,
    string? ResolvedAssemblyPath,
    string? ResolvedAssemblyName);

public sealed record ManagedReconTypeNoteFact(
    string TypeName,
    string AssemblyName,
    string ManagedObservation,
    string SaveHypothesis,
    string HypothesisStatus,
    string ManagedOnlyStatus);

public sealed record ManagedReconNotesFacts(
    ReadOnlyCollection<ManagedReconTypeNoteFact> TypeNotes);

public sealed record PrefabIdentityHypothesisResultFact(
    string HypothesisKey,
    string ManagedTypeName,
    string ValidationStatus,
    string Summary,
    ReadOnlyCollection<string> EvidenceNotes);

public sealed record PrefabIdentityHypothesisFacts(
    ReadOnlyCollection<PrefabIdentityHypothesisResultFact> Results);

public sealed record CompanyResourceHypothesisResultFact(
    string HypothesisKey,
    string ManagedTypeName,
    string ValidationStatus,
    string Summary,
    ReadOnlyCollection<string> EvidenceNotes);

public sealed record CompanyResourceHypothesisFacts(
    ReadOnlyCollection<CompanyResourceHypothesisResultFact> Results);

public sealed record CityIdentityCarrierFact(
    string CarrierKey,
    string IdentityDimension,
    string SupportStatus,
    string Summary,
    ReadOnlyCollection<string> EvidenceNotes);

public sealed record CityIdentityCarrierAuditFacts(
    ReadOnlyCollection<CityIdentityCarrierFact> Carriers);

public sealed record CityIdentityEntityFact(
    int EntityIndex,
    int BaseEntityIndex,
    string Family,
    string RelationshipStatus,
    int? PrefabRefValue,
    string? DisplayName,
    ReadOnlyCollection<string> ProvenContextDimensions,
    ReadOnlyCollection<string> UnresolvedContextDimensions,
    ReadOnlyCollection<string> EvidenceNotes);

public sealed record CityIdentityFacts(
    ReadOnlyCollection<CityIdentityEntityFact> Entities);

public sealed record CityIdentityValidationCaseFact(
    string DisplayName,
    int BaseEntityIndex,
    string ValidationKind,
    string OutcomeStatus,
    string Summary,
    ReadOnlyCollection<string> EvidenceNotes);

public sealed record CityIdentityValidationFacts(
    ReadOnlyCollection<CityIdentityValidationCaseFact> Cases);

public sealed record CityContextPromotionDimensionFact(
    string Dimension,
    string SupportStatus,
    int EntityCount,
    string Summary,
    ReadOnlyCollection<string> EvidenceNotes);

public sealed record CityContextPromotionEntityFact(
    int EntityIndex,
    int BaseEntityIndex,
    string Family,
    ReadOnlyCollection<string> ProvenContextDimensions,
    ReadOnlyCollection<string> RemainingContextDimensions,
    ReadOnlyCollection<string> EvidenceNotes);

public sealed record CityContextPromotionFacts(
    ReadOnlyCollection<CityContextPromotionDimensionFact> Dimensions,
    ReadOnlyCollection<CityContextPromotionEntityFact> Entities);

public sealed record PopulationLaborCarrierDimensionFact(
    string Dimension,
    string SupportStatus,
    int MatchingTypeCount,
    string Summary,
    ReadOnlyCollection<string> EvidenceNotes);

public sealed record PopulationLaborCarrierAuditFacts(
    ReadOnlyCollection<PopulationLaborCarrierDimensionFact> Dimensions);

public sealed record PopulationLaborSemanticGroupFact(
    string GroupKey,
    string SupportStatus,
    string Summary,
    ReadOnlyCollection<string> EvidenceNotes);

public sealed record PopulationLaborSemanticsFacts(
    ReadOnlyCollection<PopulationLaborSemanticGroupFact> Groups,
    ReadOnlyCollection<string> RemainingBlockers);

public sealed record InvestigationArtifacts(
    string SavePath,
    string ManagedPath,
    string? SavesRoot,
    IReadOnlyDictionary<string, object?>? Metadata,
    SavePreludeSummary Prelude,
    SaveInvestigationReport Report,
    TransportDomainFacts TransportDomainFacts,
    TransportFacilityClassificationAuditFacts TransportFacilityClassificationAuditFacts,
    TransportJoinPathAuditFacts TransportJoinPathAuditFacts,
    TransportStationServiceAuditFacts TransportStationServiceAuditFacts,
    TransportServiceJoinFacts TransportServiceJoinFacts,
    TransportReportFacts TransportReportFacts,
    TransportReadabilityGapFacts TransportReadabilityGapFacts,
    TransitUnderstandingFacts TransitUnderstandingFacts,
    CityUnderstandingFacts CityUnderstandingFacts,
    CityStateReportFacts CityStateReportFacts,
    BuildingDomainFacts BuildingDomainFacts,
    BuildingIdentityAuditFacts BuildingIdentityAuditFacts,
    CityIdentityCarrierAuditFacts CityIdentityCarrierAuditFacts,
    CityIdentityFacts CityIdentityFacts,
    CityIdentityValidationFacts CityIdentityValidationFacts,
    CityContextPromotionFacts CityContextPromotionFacts,
    HousingPressureFacts HousingPressureFacts,
    PopulationLaborCarrierAuditFacts PopulationLaborCarrierAuditFacts,
    PopulationLaborSemanticsFacts PopulationLaborSemanticsFacts,
    PopulationDomainFacts PopulationDomainFacts,
    LaborDomainFacts LaborDomainFacts,
    EconomyDomainFacts EconomyDomainFacts,
    CompanyHealthFacts CompanyHealthFacts,
    ExternalConnectionsFacts ExternalConnectionsFacts,
    SaveCoverageFacts SaveCoverageFacts,
    TargetedSaveFacts TargetedFacts,
    SystemBufferFacts SystemBufferFacts,
    SystemBufferCatalogFacts SystemBufferCatalogFacts,
    SystemTableFacts SystemTableFacts,
    PassengerTrainStationFacts PassengerTrainStationFacts,
    RailTransitStationFacts RailTransitStationFacts,
    TransportStationGraphFacts TransportStationGraphFacts,
    NameAndIndirectResolutionFacts NameAndIndirectResolutionFacts,
    EntityGraphFacts EntityGraphFacts,
    SerializerCatalogFacts SerializerCatalogFacts,
    PrimitiveComponentFacts PrimitiveComponentFacts,
    DynamicBufferFacts DynamicBufferFacts,
    ComponentBlockShapeFacts ComponentBlockShapeFacts,
    OuterBufferStringFacts OuterBufferStringFacts,
    EntityBacklinkFacts EntityBacklinkFacts,
    TransportLineModeAuditFacts TransportLineModeAuditFacts,
    RemainingRailIdentityFacts RemainingRailIdentityFacts,
    ConnectedNodeLayoutFacts ConnectedNodeLayoutFacts,
    RailTrackConnectivityFacts RailTrackConnectivityFacts,
    TransportTopologyFacts TransportTopologyFacts,
    ManagedReconNotesFacts ManagedReconNotesFacts,
    PrefabIdentityHypothesisFacts PrefabIdentityHypothesisFacts,
    CompanyResourceHypothesisFacts CompanyResourceHypothesisFacts,
    ReadOnlyCollection<AssemblyInventoryItem> AssemblyInventory,
    ReadOnlyCollection<CandidateTypeInventoryItem> CandidateTypes,
    ReadOnlyCollection<TypeResolutionSummary> ComponentTypeResolutions,
    ReadOnlyCollection<TypeResolutionSummary> SystemTypeResolutions);

public sealed record TransportLineFact(
    int EntityIndex,
    int ArchetypeIndex,
    int EntityOrdinal,
    int RouteNumber,
    string ColorHex,
    float VehicleInterval,
    float UnbunchingFactor,
    ushort Flags,
    ushort TicketPrice,
    int VehicleRequestEntityIndex,
    string Mode = "unresolved",
    bool? IsCargo = null,
    ReadOnlyCollection<string>? ModeEvidenceNotes = null);

public sealed record TransportLineParseResult(
    int EntityIndex,
    int ArchetypeIndex,
    int EntityOrdinal,
    int RouteNumber,
    string ColorHex,
    float VehicleInterval,
    float UnbunchingFactor,
    ushort Flags,
    ushort TicketPrice,
    int VehicleRequestEntityIndex,
    int? VehiclePrefabEntityIndex);

public sealed record WaitingPassengersStopFact(
    int EntityIndex,
    int ArchetypeIndex,
    int EntityOrdinal,
    int OwnerEntityIndex,
    int Count,
    int OngoingAccumulation,
    int ConcludedAccumulation,
    ushort SuccessAccumulation,
    ushort AverageWaitingTime);

public sealed record WaitingPassengersSummary(
    int TotalWaitingPassengers,
    int MaxWaitingPassengers,
    ReadOnlyCollection<WaitingPassengersStopFact> Stops);

public sealed record OutsideConnectionFact(
    int ArchetypeIndex,
    int EntityOrdinal,
    float Delay);

public sealed record TransportLineQueueFact(
    int EntityIndex,
    int RouteNumber,
    string ColorHex,
    int StopCount,
    int TotalWaitingPassengers,
    int MaxStopQueue);

public sealed record TargetedSaveFacts(
    ReadOnlyCollection<TransportLineFact> TransportLines,
    WaitingPassengersSummary WaitingPassengers,
    ReadOnlyCollection<OutsideConnectionFact> OutsideConnections,
    ReadOnlyCollection<TransportLineQueueFact> LineQueues);

public sealed record TransportDomainFacts(
    ReadOnlyCollection<TransportLineFact> TransportLines,
    WaitingPassengersSummary WaitingPassengers,
    ReadOnlyCollection<OutsideConnectionFact> OutsideConnections,
    ReadOnlyCollection<TransportLineQueueFact> LineQueues,
    ReadOnlyCollection<RailTransitStopOwnerFact> StopOwners,
    ReadOnlyCollection<RailTransitStationFact> RailTransitStations,
    ReadOnlyCollection<TransportFacilityFact>? TransportFacilities = null);

public sealed record TransportFacilityFact(
    string Name,
    int NameEntityIndex,
    int BaseFacilityEntityIndex,
    string Role,
    string Mode,
    string ClassificationStatus,
    ReadOnlyCollection<int> RelatedEntityIndexes,
    ReadOnlyCollection<string> EvidenceNotes);

public sealed record TransportFacilityClassificationAuditFacilityFact(
    string Name,
    int NameEntityIndex,
    int BaseFacilityEntityIndex,
    string CandidateRole,
    string CandidateMode,
    string ClassificationStatus,
    ReadOnlyCollection<int> RelatedEntityIndexes,
    ReadOnlyCollection<string> EvidenceNotes);

public sealed record TransportFacilityClassificationAuditFacts(
    ReadOnlyCollection<TransportFacilityClassificationAuditFacilityFact> Facilities);

public sealed record TransportStationServiceAuditLineFact(
    int LineEntityIndex,
    int RouteNumber,
    string ColorHex,
    string? LineName,
    string Mode,
    bool? IsCargo,
    string MatchKind,
    ReadOnlyCollection<string> EvidenceNotes);

public sealed record TransportStationServiceAuditStationFact(
    string Mode,
    string Role,
    string Name,
    int NameEntityIndex,
    int BaseStationEntityIndex,
    string JoinStatus,
    ReadOnlyCollection<int> MatchedStopOwnerEntityIndexes,
    ReadOnlyCollection<int> StationIncomingEntityIndexes,
    ReadOnlyCollection<int> CandidateLineEntityIndexes,
    ReadOnlyCollection<TransportStationServiceAuditLineFact> CandidateLines,
    ReadOnlyCollection<string> EvidenceNotes);

public sealed record TransportStationServiceAuditFacts(
    ReadOnlyCollection<TransportStationServiceAuditStationFact> Stations);

public sealed record TransportJoinPathAuditCarrierFact(
    int JoinEntityIndex,
    string JoinEntityRole,
    string CarrierKind,
    string JoinComponentType,
    ReadOnlyCollection<int> CandidateLineEntityIndexes,
    ReadOnlyCollection<string> EvidenceNotes);

public sealed record TransportJoinPathAuditStationFact(
    string Mode,
    string Role,
    string Name,
    int NameEntityIndex,
    int BaseStationEntityIndex,
    string JoinStatus,
    ReadOnlyCollection<TransportJoinPathAuditCarrierFact> CandidateCarriers,
    ReadOnlyCollection<string> EvidenceNotes);

public sealed record TransportJoinPathAuditBufferFact(
    int ArchetypeIndex,
    int EntityCount,
    string JoinComponentType,
    int TrailingByteCount,
    int ResolvedEntityCount,
    ReadOnlyCollection<string> EvidenceNotes);

public sealed record TransportJoinModeCoverageFact(
    string Mode,
    string CoverageStatus,
    int StationGroupCount,
    int SolvedStationGroupCount,
    int CheckedStopCount,
    int ExactCarrierStopCount,
    ReadOnlyCollection<string> EvidenceNotes);

public sealed record TransportJoinPathAuditFacts(
    ReadOnlyCollection<TransportJoinPathAuditStationFact> Stations,
    ReadOnlyCollection<TransportJoinPathAuditBufferFact> RouteBuffers,
    ReadOnlyCollection<TransportJoinModeCoverageFact> ModeCoverage);

public sealed record TransportServiceJoinLineFact(
    int LineEntityIndex,
    int RouteNumber,
    string ColorHex,
    string? LineName,
    string JoinComponentType,
    ReadOnlyCollection<string> EvidenceNotes);

public sealed record TransportServiceJoinStationFact(
    string Mode,
    string Role,
    string Name,
    int NameEntityIndex,
    int BaseStationEntityIndex,
    string JoinStatus,
    ReadOnlyCollection<int> ExactLineEntityIndexes,
    ReadOnlyCollection<TransportServiceJoinLineFact> ExactLines,
    ReadOnlyCollection<string> EvidenceNotes);

public sealed record TransportServiceJoinBufferFact(
    int ArchetypeIndex,
    int EntityCount,
    string JoinComponentType,
    int TrailingByteCount,
    int ResolvedEntityCount,
    ReadOnlyCollection<string> EvidenceNotes);

public sealed record TransportServiceJoinFacts(
    ReadOnlyCollection<TransportServiceJoinStationFact> Stations,
    ReadOnlyCollection<TransportServiceJoinBufferFact> RouteBuffers,
    ReadOnlyCollection<TransportJoinModeCoverageFact> ModeCoverage);

public sealed record TransportReportLineFact(
    string DisplayName,
    int LineEntityIndex,
    int RouteNumber,
    string ColorHex,
    string Mode,
    bool? IsCargo,
    ReadOnlyCollection<string> EvidenceNotes);

public sealed record TransportReportLineGroupFact(
    string Mode,
    int LineCount,
    ReadOnlyCollection<TransportReportLineFact> Lines);

public sealed record TransportReportStationFact(
    string Name,
    string Mode,
    string ServiceJoinStatus,
    ReadOnlyCollection<string> ServedLineNames,
    ReadOnlyCollection<string> CandidateLineNames,
    ReadOnlyCollection<string> PlatformNames,
    ReadOnlyCollection<string> EntranceNames,
    ReadOnlyCollection<string>? OutsideDestinationNames = null,
    string? Role = null,
    string? ClassificationStatus = null);

public sealed record TransportReportStationGroupFact(
    string Mode,
    int StationCount,
    ReadOnlyCollection<TransportReportStationFact> Stations);

public sealed record TransportReportQueueHotspotFact(
    string LineDisplayName,
    int LineEntityIndex,
    int RouteNumber,
    string ColorHex,
    int TotalWaitingPassengers,
    int MaxStopQueue);

public sealed record TransportReportJoinCoverageFact(
    string Mode,
    string CoverageStatus,
    string Summary);

public sealed record TransportReportFacts(
    int TotalLineCount,
    int TotalStationEntityCount,
    ReadOnlyCollection<TransportReportLineGroupFact> LineGroups,
    ReadOnlyCollection<TransportReportStationGroupFact> StationGroups,
    ReadOnlyCollection<TransportReportQueueHotspotFact> TopQueueHotspots,
    ReadOnlyCollection<TransportReportJoinCoverageFact> JoinCoverage);

public sealed record TransportReadabilityGapFact(
    string Category,
    string Severity,
    string Summary,
    int AffectedCount,
    ReadOnlyCollection<string> Examples);

public sealed record TransportReadabilityGapFacts(
    ReadOnlyCollection<TransportReadabilityGapFact> Gaps);

public sealed record TransportLineModeAuditStopFamilyClueFact(
    string StopFamily,
    int StopCount,
    bool HasOutsideConnectionClue,
    ReadOnlyCollection<int> StopEntityIndexes,
    ReadOnlyCollection<int> OwnerEntityIndexes,
    ReadOnlyCollection<string> OwnerComponentTypes);

public sealed record TransportLineModeAuditLineFact(
    int LineEntityIndex,
    int ArchetypeIndex,
    int RouteNumber,
    string ColorHex,
    string CandidateMode,
    string? VehicleModeClue,
    int? VehiclePrefabEntityIndex,
    ReadOnlyCollection<string> VehiclePrefabComponentTypes,
    bool? IsCargo,
    bool HasCargoOwnerClue,
    bool HasOutsideConnectionClue,
    ReadOnlyCollection<TransportLineModeAuditStopFamilyClueFact> StopFamilyClues);

public sealed record TransportLineModeAuditFacts(
    ReadOnlyCollection<TransportLineModeAuditLineFact> Lines);

public sealed record RemainingRailIdentityStationJoinFact(
    string StationName,
    string StationMode,
    string JoinStatus,
    string MatchKind,
    ReadOnlyCollection<string> EvidenceNotes);

public sealed record RemainingRailIdentityLineFact(
    string DisplayName,
    int LineEntityIndex,
    int RouteNumber,
    string ColorHex,
    string VehicleModeClue,
    int? VehiclePrefabEntityIndex,
    ReadOnlyCollection<string> VehiclePrefabComponentTypes,
    ReadOnlyCollection<string> StopFamilies,
    ReadOnlyCollection<RemainingRailIdentityStationJoinFact> CandidateStationJoins,
    ReadOnlyCollection<string> ExclusionNotes,
    ReadOnlyCollection<string> CandidateModes);

public sealed record RemainingRailIdentityFacts(
    int UnresolvedLineCount,
    ReadOnlyCollection<string> TramComparisonPrefabKeys,
    ReadOnlyCollection<int> TrainCandidateLineEntityIndexes,
    ReadOnlyCollection<string> SubwayStationNames,
    ReadOnlyCollection<RemainingRailIdentityLineFact> Lines);

public sealed record BuildingDomainBuildingFact(
    int EntityIndex,
    int ArchetypeIndex,
    int EntityOrdinal,
    int? OwnerEntityIndex,
    ReadOnlyCollection<int> BuildingOwnerChainEntityIndexes,
    int? PrefabRefValue,
    bool HasCustomName,
    string? CustomName,
    ReadOnlyCollection<string> BuildingComponentTypes,
    ReadOnlyCollection<string> ServiceComponentTypes);

public sealed record BuildingDomainFacts(
    ReadOnlyCollection<BuildingDomainBuildingFact> Buildings);

public sealed record BuildingIdentityAuditBuildingFact(
    int EntityIndex,
    int BaseBuildingEntityIndex,
    int? PrefabRefValue,
    bool HasCustomName,
    string? DisplayName,
    string AddressStatus,
    string StreetStatus,
    ReadOnlyCollection<string> EvidenceNotes);

public sealed record BuildingIdentityAuditFacts(
    ReadOnlyCollection<BuildingIdentityAuditBuildingFact> Buildings);

public sealed record CitizenRoleSummaryFact(
    int TotalCitizens,
    int HouseholdMembers,
    int Workers,
    int Students);

public sealed record HouseholdRoleSummaryFact(
    int TotalHouseholds,
    int CommuterHouseholds,
    int TouristHouseholds,
    int HomelessHouseholds);

public sealed record LaborMarketSummaryFact(
    int WorkerCitizens,
    int StudentCitizens,
    int WorkerStudents,
    int CitizensWithoutWorkerOrStudentRole);

public sealed record CitizenArchetypePopulationFact(
    int ArchetypeIndex,
    int EntityCount,
    bool HasHouseholdMember,
    bool HasWorker,
    bool HasStudent,
    bool HasCurrentBuilding,
    bool HasCurrentTransport,
    bool HasTravelPurpose);

public sealed record HouseholdArchetypePopulationFact(
    int ArchetypeIndex,
    int EntityCount,
    bool IsCommuterHousehold,
    bool IsTouristHousehold,
    bool IsHomelessHousehold);

public sealed record PopulationDomainFacts(
    CitizenRoleSummaryFact CitizenSummary,
    HouseholdRoleSummaryFact HouseholdSummary,
    LaborMarketSummaryFact LaborSummary,
    ReadOnlyCollection<CitizenArchetypePopulationFact> CitizenArchetypes,
    ReadOnlyCollection<HouseholdArchetypePopulationFact> HouseholdArchetypes);

public sealed record HousingPressureFacts(
    int TotalHouseholds,
    int TotalBuildings,
    int NamedBuildings,
    int ResidentialBuildingCandidates,
    string ActionabilityStatus,
    string Summary,
    ReadOnlyCollection<string> EvidenceNotes,
    ReadOnlyCollection<string> RemainingBlockers);

public sealed record LaborDomainFacts(
    int TotalWorkers,
    int TotalStudents,
    int WorkerStudents,
    int CitizensWithoutWorkerOrStudentRole,
    string ActionabilityStatus,
    string Summary,
    ReadOnlyCollection<string> EvidenceNotes,
    ReadOnlyCollection<string> RemainingBlockers);

public sealed record CompanyHealthFacts(
    int TotalCompanies,
    int TransportCompanies,
    int ResourceTaggedEntities,
    int OutsideConnections,
    int OfficeBuildingCandidates,
    int CommercialBuildingCandidates,
    int IndustrialBuildingCandidates,
    string ProfitabilityStatus,
    string ActionabilityStatus,
    string Summary,
    ReadOnlyCollection<string> EvidenceNotes,
    ReadOnlyCollection<string> RemainingBlockers);

public sealed record ExternalConnectionsFacts(
    int TotalOutsideConnections,
    int CargoTransportLines,
    int TransportOutsideConnections,
    int EconomyOutsideConnections,
    string FlowStatus,
    string ActionabilityStatus,
    string Summary,
    ReadOnlyCollection<string> EvidenceNotes,
    ReadOnlyCollection<string> RemainingBlockers);

public sealed record EconomyStructureSummaryFact(
    int CompanyEntities,
    int ResourceTaggedEntities,
    int WorkProviders,
    int Employees,
    int TransportCompanies,
    int IndustrialCompanies,
    int CommercialCompanies,
    int OfficeCompanies,
    int ExtractorCompanies,
    int StorageCompanies,
    int ResourceBuyers,
    int ResourceSellers,
    int TradeCostEntities,
    int OutsideConnections);

public sealed record CompanyArchetypeEconomyFact(
    int ArchetypeIndex,
    int EntityCount,
    bool IsTransportCompany,
    bool IsIndustrialCompany,
    bool IsCommercialCompany,
    bool IsOfficeCompany,
    bool IsExtractorCompany,
    bool IsStorageCompany,
    bool HasResources,
    bool HasWorkProvider,
    bool HasEmployee,
    bool HasTradeCost,
    bool HasResourceBuyer,
    bool HasResourceSeller);

public sealed record OutsideConnectionEconomyArchetypeFact(
    int ArchetypeIndex,
    int EntityCount);

public sealed record EconomyDomainFacts(
    EconomyStructureSummaryFact Summary,
    ReadOnlyCollection<CompanyArchetypeEconomyFact> CompanyArchetypes,
    ReadOnlyCollection<OutsideConnectionEconomyArchetypeFact> OutsideConnectionArchetypes);

public sealed record CoverageFamilyFact(
    string Kind,
    string FamilyName,
    string Status,
    int TypeCount,
    int DecodedTypeCount,
    ReadOnlyCollection<string> ExampleTypes,
    string? Notes);

public sealed record CoverageOpenItemFact(
    string Status,
    string Scope,
    string Description);

public sealed record SaveCoverageFacts(
    ReadOnlyCollection<CoverageFamilyFact> ComponentFamilies,
    ReadOnlyCollection<CoverageFamilyFact> SystemFamilies,
    ReadOnlyCollection<CoverageOpenItemFact> OpenItems);

public sealed record SystemBufferFact(
    int Index,
    string TypeName,
    int UncompressedSize,
    int CompressedSize);

public sealed record NameSystemCandidateNameFact(
    string Value,
    int StringOffset,
    ReadOnlyCollection<int> NearbyEntityIndexes,
    ReadOnlyCollection<int> NearbyTransportLineEntityIndexes);

public sealed record RawPrintableStringFact(
    string Value,
    int StringOffset);

public sealed record NameSystemEntryFact(
    int EntityIndex,
    string Value,
    int StringOffset);

public sealed record NameSystemFact(
    int SystemIndex,
    string TypeName,
    int UncompressedSize,
    int CompressedSize,
    ReadOnlyCollection<RawPrintableStringFact> RawPrintableStrings,
    ReadOnlyCollection<NameSystemEntryFact> Entries,
    ReadOnlyCollection<NameSystemCandidateNameFact> CandidateNames,
    ReadOnlyCollection<NameSystemCandidateNameFact> MatchedTransportLineNames);

public sealed record SystemBufferFacts(
    ReadOnlyCollection<SystemBufferFact> SystemBuffers,
    NameSystemFact? NameSystem);

public sealed record SystemBufferCatalogStringFact(
    string Encoding,
    string Value,
    int Offset);

public sealed record SystemBufferCatalogEntityLikeIntFact(
    int Offset,
    int Value);

public sealed record SystemBufferCatalogSystemFact(
    int SystemIndex,
    string TypeName,
    int UncompressedSize,
    int CompressedSize,
    string ShapeClassification,
    int ReadableStringCount,
    int EntityLikeIntPatternCount,
    ReadOnlyCollection<SystemBufferCatalogStringFact> SampleStrings,
    ReadOnlyCollection<SystemBufferCatalogEntityLikeIntFact> SampleEntityLikeInts);

public sealed record SystemBufferCatalogFacts(
    ReadOnlyCollection<SystemBufferCatalogSystemFact> Systems);

public sealed record SystemTableReviewFact(
    int SystemIndex,
    string TypeName,
    string ShapeClassification,
    string Resolution,
    string? DecoderKind,
    int? DecodedEntryCount,
    string? Notes);

public sealed record NameSystemTableFact(
    int SystemIndex,
    string TypeName,
    int UncompressedSize,
    int CompressedSize,
    ReadOnlyCollection<NameSystemEntryFact> Entries);

public sealed record SystemTableFacts(
    ReadOnlyCollection<SystemTableReviewFact> ReviewedSystems,
    NameSystemTableFact? NameSystem);

public sealed record PassengerTrainStationStopFact(
    int EntityIndex,
    int ArchetypeIndex,
    int EntityOrdinal,
    int OwnerEntityIndex);

public sealed record PassengerTrainStationServedLineFact(
    int LineEntityIndex,
    int RouteNumber,
    string ColorHex,
    string? LineName,
    ReadOnlyCollection<string> EvidenceComponentTypes);

public sealed record PassengerTrainStationFact(
    string Name,
    int NameEntityIndex,
    int NameStringOffset,
    int OwnerEntityIndex,
    ReadOnlyCollection<int> StopEntityIndexes,
    ReadOnlyCollection<PassengerTrainStationServedLineFact> ServedLines);

public sealed record PassengerTrainStationFacts(
    ReadOnlyCollection<PassengerTrainStationStopFact> Stops,
    ReadOnlyCollection<PassengerTrainStationFact> Stations);

public sealed record RailTransitStopOwnerFact(
    string Mode,
    int StopEntityIndex,
    int StopArchetypeIndex,
    int StopEntityOrdinal,
    int OwnerEntityIndex,
    int AttachedEntityIndex,
    bool IsOutsideConnection = false);

public sealed record RailTransitServedLineFact(
    int LineEntityIndex,
    int RouteNumber,
    string ColorHex,
    string? LineName,
    ReadOnlyCollection<string> EvidenceComponentTypes);

public sealed record RailTransitStationFact(
    string Mode,
    string Role,
    string Name,
    int NameEntityIndex,
    int NameStringOffset,
    int OwnerEntityIndex,
    int BaseStationEntityIndex,
    string? BaseStationName,
    ReadOnlyCollection<int> MatchedStopOwnerEntityIndexes,
    ReadOnlyCollection<RailTransitServedLineFact> ServedLines,
    string ServiceJoinStatus = "unresolved",
    ReadOnlyCollection<RailTransitServedLineFact>? CandidateLines = null);

public sealed record RailTransitStationFacts(
    ReadOnlyCollection<RailTransitStopOwnerFact> StopOwners,
    ReadOnlyCollection<RailTransitStationFact> Stations);

public sealed record ResolvedLineNameFact(
    int TargetEntityIndex,
    int RouteNumber,
    string ColorHex,
    string Value,
    int NameEntityIndex,
    int StringOffset);

public sealed record ResolvedEntityNameFact(
    string Mode,
    string Role,
    int TargetEntityIndex,
    string Value,
    int NameEntityIndex,
    int StringOffset);

public sealed record NameAndIndirectResolutionFacts(
    ReadOnlyCollection<ResolvedLineNameFact> LineNames,
    ReadOnlyCollection<ResolvedEntityNameFact> RailTransitNames,
    ReadOnlyCollection<ResolvedEntityNameFact> PassengerTrainNames);

public sealed record EntityGraphReferenceComponentFact(
    int ComponentIndex,
    string TypeName);

public sealed record EntityGraphEdgeFact(
    string EdgeKind,
    int SourceEntityIndex,
    int SourceArchetypeIndex,
    int SourceEntityOrdinal,
    int TargetEntityIndex,
    int? TargetArchetypeIndex,
    int SourceComponentIndex);

public sealed record EntityGraphBacklinkFact(
    int TargetEntityIndex,
    int? TargetArchetypeIndex,
    ReadOnlyCollection<int> IncomingEdgeIndexes);

public sealed record EntityGraphFacts(
    ReadOnlyCollection<EntityGraphReferenceComponentFact> ReferenceComponents,
    ReadOnlyCollection<EntityGraphEdgeFact> Edges,
    ReadOnlyCollection<EntityGraphBacklinkFact> Backlinks);

public sealed record TransportStationGraphNodeFact(
    int EntityIndex,
    int ArchetypeIndex,
    int OwnerEntityIndex,
    bool HasCustomName,
    string? MatchedName,
    ReadOnlyCollection<string> ComponentTypes);

public sealed record TransportStationGraphFact(
    string Mode,
    int StationEntityIndex,
    int ArchetypeIndex,
    bool IsCargoStation,
    bool HasCustomName,
    string? MatchedName,
    ReadOnlyCollection<string> ComponentTypes,
    ReadOnlyCollection<TransportStationGraphNodeFact> OwnedEntities,
    ReadOnlyCollection<TransportStationGraphNamedDescendantFact> NamedDescendants);

public sealed record TransportStationGraphNamedDescendantFact(
    int EntityIndex,
    int ArchetypeIndex,
    int OwnerEntityIndex,
    int Depth,
    string MatchedName,
    ReadOnlyCollection<int> OwnerChainToStation,
    ReadOnlyCollection<string> ComponentTypes);

public sealed record TrainStopOwnerChainFact(
    int StopOwnerEntityIndex,
    ReadOnlyCollection<TransportStationGraphNodeFact> Chain);

public sealed record TransportStationGraphFacts(
    ReadOnlyCollection<TransportStationGraphFact> Stations,
    ReadOnlyCollection<TrainStopOwnerChainFact> TrainStopOwnerChains);

public sealed record SerializerCatalogTypeHint(
    bool Resolved,
    string? ResolvedAssemblyName,
    string? ResolvedAssemblyPath,
    string ManagedTypeShape,
    bool ImplementsBufferElementData);

public sealed record SerializerCatalogBlockShapeFact(
    int ArchetypeIndex,
    int EntityCount,
    int BlockLength,
    int? BytesPerEntity);

public sealed record SerializerCatalogComponentFact(
    int ComponentIndex,
    string TypeName,
    byte SerializerType,
    int EntityCoverage,
    int ArchetypeCount,
    ReadOnlyCollection<SerializerCatalogBlockShapeFact> BlockShapes,
    SerializerCatalogTypeHint TypeHint);

public sealed record SerializerCatalogFacts(
    ReadOnlyCollection<SerializerCatalogComponentFact> Components);

public sealed record PrimitiveDecoderKeyFact(
    byte SerializerType,
    string BlockShape,
    int? BytesPerEntity,
    string ManagedTypeShape);

public sealed record PrimitiveComponentSampleFact(
    int EntityIndex,
    int ArchetypeIndex,
    int EntityOrdinal,
    string RawHex,
    int? LeadingInt32);

public sealed record PrimitiveDecoderVariantFact(
    PrimitiveDecoderKeyFact DecoderKey,
    string DecoderKind,
    ReadOnlyCollection<PrimitiveComponentSampleFact> Samples);

public sealed record PrimitiveComponentFact(
    int ComponentIndex,
    string TypeName,
    int EntityCoverage,
    int ArchetypeCount,
    ReadOnlyCollection<PrimitiveDecoderVariantFact> DecoderVariants);

public sealed record PrimitiveComponentFacts(
    ReadOnlyCollection<PrimitiveComponentFact> Components);

public sealed record DynamicBufferStructuredValueFact(
    string FieldName,
    string Value);

public sealed record DynamicBufferEntryFact(
    int EntryOrdinal,
    string RawHex,
    ReadOnlyCollection<DynamicBufferStructuredValueFact> StructuredValues);

public sealed record DynamicBufferEntityFact(
    int EntityIndex,
    int ArchetypeIndex,
    int EntityOrdinal,
    int HeaderValue,
    int ElementCount,
    ReadOnlyCollection<DynamicBufferEntryFact> Entries);

public sealed record DynamicBufferBlockFact(
    int ComponentIndex,
    string TypeName,
    byte SerializerType,
    int ArchetypeIndex,
    int EntityCount,
    int BlockLength,
    ReadOnlyCollection<DynamicBufferEntityFact> Entities);

public sealed record DynamicBufferFacts(
    ReadOnlyCollection<DynamicBufferBlockFact> Blocks);

public sealed record ComponentBlockShapeFact(
    int ComponentIndex,
    int ComponentOrdinal,
    string ComponentTypeName,
    byte SerializerType,
    int BlockLength,
    int? BytesPerEntity,
    string LeadingHex);

public sealed record ComponentBlockShapeArchetypeFact(
    int ArchetypeIndex,
    int EntityCount,
    bool IsTrainStationArchetype,
    bool IsTrainStopArchetype,
    ReadOnlyCollection<ComponentBlockShapeFact> Components);

public sealed record ComponentBlockShapeFacts(
    ReadOnlyCollection<ComponentBlockShapeArchetypeFact> Archetypes);

public sealed record OuterBufferStringFact(
    string Encoding,
    string Value,
    int ByteOffset);

public sealed record OuterBufferStringSourceFact(
    string SourceKind,
    int SourceIndex,
    string SourceLabel,
    int UncompressedSize,
    int? EntityCount,
    bool IsTrainStationArchetype,
    bool IsTrainStopArchetype,
    ReadOnlyCollection<string> ComponentTypes,
    ReadOnlyCollection<OuterBufferStringFact> Strings);

public sealed record OuterBufferStringFacts(
    ReadOnlyCollection<OuterBufferStringSourceFact> Sources);

public sealed record EntityBacklinkTargetFact(
    int TargetEntityIndex,
    int TargetArchetypeIndex);

public sealed record EntityBacklinkMatchFact(
    int TargetEntityIndex,
    int SourceEntityIndex,
    int SourceArchetypeIndex,
    int SourceEntityOrdinal,
    int SourceComponentIndex,
    int SourceComponentOrdinal,
    string SourceComponentTypeName,
    byte SerializerType,
    int BytesPerEntity,
    int FieldOffset);

public sealed record EntityBacklinkFacts(
    ReadOnlyCollection<EntityBacklinkTargetFact> Targets,
    ReadOnlyCollection<EntityBacklinkMatchFact> Matches);

public sealed record ConnectedNodeFlatStrideCandidateFact(
    bool MatchesLayout,
    int? BytesPerEntity,
    ReadOnlyCollection<RailTrackConnectedNodeFact> Entries);

public sealed record ConnectedNodeCountPrefixedEntityFact(
    int EntityOrdinal,
    int CandidateCount,
    int HeaderValue,
    int EntryDataOffset,
    ReadOnlyCollection<RailTrackConnectedNodeFact> Entries);

public sealed record ConnectedNodeCountPrefixedCandidateFact(
    bool MatchesLayout,
    int BytesConsumed,
    ReadOnlyCollection<ConnectedNodeCountPrefixedEntityFact> Entities);

public sealed record ConnectedNodeLayoutArchetypeFact(
    int ArchetypeIndex,
    int EntityCount,
    int ComponentIndex,
    int ComponentOrdinal,
    string ComponentTypeName,
    int BlockLength,
    int? BytesPerEntity,
    string LeadingHex,
    ReadOnlyCollection<int> LeadingInt32Values,
    string LikelyLayout,
    ConnectedNodeFlatStrideCandidateFact FlatStrideCandidate,
    ConnectedNodeCountPrefixedCandidateFact CountPrefixedCandidate);

public sealed record ConnectedNodeLayoutFacts(
    ReadOnlyCollection<ConnectedNodeLayoutArchetypeFact> Archetypes);

public sealed record RailTrackConnectedNodeFact(
    int NodeEntityIndex,
    float CurvePosition);

public sealed record RailTrackConnectivityEdgeFact(
    int EdgeEntityIndex,
    int ArchetypeIndex,
    int EntityOrdinal,
    int OwnerEntityIndex,
    int StartNodeEntityIndex,
    int EndNodeEntityIndex,
    ReadOnlyCollection<int> AttachedStopEntityIndexes,
    ReadOnlyCollection<int> AttachedStopOwnerEntityIndexes)
{
    public ReadOnlyCollection<RailTrackConnectedNodeFact> ConnectedNodes { get; init; } =
        new([]);
}

public sealed record RailTrackConnectivityFacts(
    ReadOnlyCollection<RailTrackConnectivityEdgeFact> Edges);

public sealed record TransportTopologyOutsideTrainStopFact(
    string Name,
    int StopEntityIndex,
    int OwnerEntityIndex,
    int AttachedEntityIndex);

public sealed record TransportTopologyPlatformFact(
    string PlatformName,
    string BaseStationName,
    int PlatformEntityIndex,
    int BaseStationEntityIndex,
    string TopologyStatus,
    ReadOnlyCollection<int> AttachedEdgeEntityIndexes,
    ReadOnlyCollection<int> ConnectedNodeEntityIndexes,
    ReadOnlyCollection<string> MatchedOutsideStopNames,
    ReadOnlyCollection<string> EvidenceNotes);

public sealed record TransportTopologyFacts(
    ReadOnlyCollection<TransportTopologyOutsideTrainStopFact> OutsideTrainStops,
    ReadOnlyCollection<TransportTopologyPlatformFact> Platforms);

public sealed record TransitUnderstandingCategoryFact(
    string Category,
    string Status,
    int SolvedCount,
    int TotalCount,
    int Weight,
    int EarnedWeight,
    string Summary,
    ReadOnlyCollection<string> RemainingBlockers);

public sealed record TransitUnderstandingFacts(
    int EstimatedCompletionPercent,
    ReadOnlyCollection<TransitUnderstandingCategoryFact> Categories,
    ReadOnlyCollection<string> RemainingBlockers);

public sealed record CityUnderstandingFacts(
    int EstimatedCompletionPercent,
    ReadOnlyCollection<CityUnderstandingDomainFact> Domains,
    ReadOnlyCollection<string> RemainingBlockers);

public sealed record CityStateReportFacts(
    int EstimatedCompletionPercent,
    ReadOnlyCollection<CityStateReportSectionFact> Sections);

public sealed record CityStateReportSectionFact(
    string Title,
    string Summary,
    string ActionabilityStatus,
    ReadOnlyCollection<string> EvidenceNotes,
    ReadOnlyCollection<string> RemainingBlockers);

public sealed record CityUnderstandingDomainFact(
    string Domain,
    string ActionabilityStatus,
    int CoveragePercent,
    int ReliabilityPercent,
    int ActionabilityPercent,
    string Summary,
    ReadOnlyCollection<string> EvidenceNotes,
    ReadOnlyCollection<string> RemainingBlockers);

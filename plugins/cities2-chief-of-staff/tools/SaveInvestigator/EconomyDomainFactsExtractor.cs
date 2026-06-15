using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class EconomyDomainFactsExtractor
{
    private const string CompanyDataTypeName = "Game.Companies.CompanyData";
    private const string TransportCompanyTypeName = "Game.Companies.TransportCompany";
    private const string IndustrialCompanyTypeName = "Game.Companies.IndustrialCompany";
    private const string CommercialCompanyTypeName = "Game.Companies.CommercialCompany";
    private const string OfficeCompanyTypeName = "Game.Companies.OfficeCompany";
    private const string ExtractorCompanyTypeName = "Game.Companies.ExtractorCompany";
    private const string StorageCompanyTypeName = "Game.Companies.StorageCompany";
    private const string WorkProviderTypeName = "Game.Companies.WorkProvider";
    private const string EmployeeTypeName = "Game.Companies.Employee";
    private const string ResourcesTypeName = "Game.Economy.Resources";
    private const string TradeCostTypeName = "Game.Companies.TradeCost";
    private const string ResourceBuyerTypeName = "Game.Companies.ResourceBuyer";
    private const string ResourceSellerTypeName = "Game.Companies.ResourceSeller";
    private const string OutsideConnectionTypeName = "Game.Net.OutsideConnection";

    public static EconomyDomainFacts Extract(SavePreludeSummary summary)
    {
        var companyArchetypes = new List<CompanyArchetypeEconomyFact>();
        var outsideConnectionArchetypes = new List<OutsideConnectionEconomyArchetypeFact>();

        foreach (var archetype in summary.Archetypes)
        {
            var componentTypes = archetype.ComponentTypeIndexes
                .Select(index => summary.ComponentTypes[index].TypeName)
                .ToHashSet(StringComparer.Ordinal);

            if (SerializedTypeMatcher.Contains(componentTypes, CompanyDataTypeName))
            {
                companyArchetypes.Add(
                    new CompanyArchetypeEconomyFact(
                        archetype.Index,
                        archetype.EntityCount,
                        SerializedTypeMatcher.Contains(componentTypes, TransportCompanyTypeName),
                        SerializedTypeMatcher.Contains(componentTypes, IndustrialCompanyTypeName),
                        SerializedTypeMatcher.Contains(componentTypes, CommercialCompanyTypeName),
                        SerializedTypeMatcher.Contains(componentTypes, OfficeCompanyTypeName),
                        SerializedTypeMatcher.Contains(componentTypes, ExtractorCompanyTypeName),
                        SerializedTypeMatcher.Contains(componentTypes, StorageCompanyTypeName),
                        SerializedTypeMatcher.Contains(componentTypes, ResourcesTypeName),
                        SerializedTypeMatcher.Contains(componentTypes, WorkProviderTypeName),
                        SerializedTypeMatcher.Contains(componentTypes, EmployeeTypeName),
                        SerializedTypeMatcher.Contains(componentTypes, TradeCostTypeName),
                        SerializedTypeMatcher.Contains(componentTypes, ResourceBuyerTypeName),
                        SerializedTypeMatcher.Contains(componentTypes, ResourceSellerTypeName)));
            }

            if (SerializedTypeMatcher.Contains(componentTypes, OutsideConnectionTypeName))
            {
                outsideConnectionArchetypes.Add(
                    new OutsideConnectionEconomyArchetypeFact(
                        archetype.Index,
                        archetype.EntityCount));
            }
        }

        var companyEntities = companyArchetypes.Sum(archetype => archetype.EntityCount);
        var resourceTaggedEntities = summary.Archetypes
            .Where(archetype => ArchetypeContains(summary, archetype, ResourcesTypeName))
            .Sum(archetype => archetype.EntityCount);
        var workProviders = companyArchetypes
            .Where(archetype => archetype.HasWorkProvider)
            .Sum(archetype => archetype.EntityCount);
        var employees = companyArchetypes
            .Where(archetype => archetype.HasEmployee)
            .Sum(archetype => archetype.EntityCount);
        var transportCompanies = companyArchetypes
            .Where(archetype => archetype.IsTransportCompany)
            .Sum(archetype => archetype.EntityCount);
        var industrialCompanies = companyArchetypes
            .Where(archetype => archetype.IsIndustrialCompany)
            .Sum(archetype => archetype.EntityCount);
        var commercialCompanies = companyArchetypes
            .Where(archetype => archetype.IsCommercialCompany)
            .Sum(archetype => archetype.EntityCount);
        var officeCompanies = companyArchetypes
            .Where(archetype => archetype.IsOfficeCompany)
            .Sum(archetype => archetype.EntityCount);
        var extractorCompanies = companyArchetypes
            .Where(archetype => archetype.IsExtractorCompany)
            .Sum(archetype => archetype.EntityCount);
        var storageCompanies = companyArchetypes
            .Where(archetype => archetype.IsStorageCompany)
            .Sum(archetype => archetype.EntityCount);
        var resourceBuyers = companyArchetypes
            .Where(archetype => archetype.HasResourceBuyer)
            .Sum(archetype => archetype.EntityCount);
        var resourceSellers = companyArchetypes
            .Where(archetype => archetype.HasResourceSeller)
            .Sum(archetype => archetype.EntityCount);
        var tradeCostEntities = companyArchetypes
            .Where(archetype => archetype.HasTradeCost)
            .Sum(archetype => archetype.EntityCount);
        var outsideConnections = outsideConnectionArchetypes.Sum(archetype => archetype.EntityCount);

        return new EconomyDomainFacts(
            new EconomyStructureSummaryFact(
                companyEntities,
                resourceTaggedEntities,
                workProviders,
                employees,
                transportCompanies,
                industrialCompanies,
                commercialCompanies,
                officeCompanies,
                extractorCompanies,
                storageCompanies,
                resourceBuyers,
                resourceSellers,
                tradeCostEntities,
                outsideConnections),
            new ReadOnlyCollection<CompanyArchetypeEconomyFact>(
                companyArchetypes
                    .OrderBy(archetype => archetype.ArchetypeIndex)
                    .ToList()),
            new ReadOnlyCollection<OutsideConnectionEconomyArchetypeFact>(
                outsideConnectionArchetypes
                    .OrderBy(archetype => archetype.ArchetypeIndex)
                    .ToList()));
    }

    private static bool ArchetypeContains(SavePreludeSummary summary, ArchetypeSummary archetype, string typeName)
    {
        return archetype.ComponentTypeIndexes
            .Select(index => summary.ComponentTypes[index].TypeName)
            .Any(componentType => SerializedTypeMatcher.Matches(componentType, typeName));
    }
}

using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class CompanyResourceHypothesisFactsExtractor
{
    public static CompanyResourceHypothesisFacts Extract(
        EconomyDomainFacts economyDomainFacts,
        SerializerCatalogFacts serializerCatalogFacts,
        ManagedReconNotesFacts managedReconNotesFacts)
    {
        var results = new List<CompanyResourceHypothesisResultFact>
        {
            BuildBufferCarrierResult(
                "company_resources_buffer",
                "Game.Economy.Resources",
                economyDomainFacts.Summary.ResourceTaggedEntities,
                serializerCatalogFacts.Components,
                managedReconNotesFacts),
            BuildBufferCarrierResult(
                "company_trade_cost_buffer",
                "Game.Companies.TradeCost",
                economyDomainFacts.Summary.TradeCostEntities,
                serializerCatalogFacts.Components,
                managedReconNotesFacts),
            BuildComponentCarrierResult(
                "company_resource_buyer_component",
                "Game.Companies.ResourceBuyer",
                economyDomainFacts.Summary.ResourceBuyers,
                serializerCatalogFacts.Components,
                managedReconNotesFacts),
            BuildTagClueResult(
                "company_resource_seller_tag_clue",
                "Game.Companies.ResourceSeller",
                economyDomainFacts.Summary.ResourceSellers,
                serializerCatalogFacts.Components,
                managedReconNotesFacts)
        };

        return new CompanyResourceHypothesisFacts(
            new ReadOnlyCollection<CompanyResourceHypothesisResultFact>(results));
    }

    private static CompanyResourceHypothesisResultFact BuildBufferCarrierResult(
        string hypothesisKey,
        string typeName,
        int entityCoverage,
        IReadOnlyCollection<SerializerCatalogComponentFact> serializerComponents,
        ManagedReconNotesFacts managedReconNotesFacts)
    {
        var note = managedReconNotesFacts.TypeNotes.First(item => string.Equals(item.TypeName, typeName, StringComparison.Ordinal));
        var component = FindComponent(serializerComponents, typeName);
        var isConfirmed = component is not null &&
                          component.TypeHint.ImplementsBufferElementData &&
                          string.Equals(component.TypeHint.ManagedTypeShape, "buffer_element", StringComparison.Ordinal) &&
                          entityCoverage > 0;
        var validationStatus = isConfirmed
            ? "confirmed"
            : component is not null || entityCoverage > 0
                ? "inconclusive"
                : "disproved";
        var summary = validationStatus switch
        {
            "confirmed" => $"{typeName} is a real save-backed buffer carrier with live entity coverage.",
            "inconclusive" => $"{typeName} is present in managed or save-side evidence, but the current sample does not fully confirm it as a useful save carrier.",
            _ => $"{typeName} did not produce enough managed/save overlap to support the hypothesis."
        };
        var evidenceNotes = new List<string>
        {
            $"managed_observation:{note.ManagedObservation}",
            $"coverage:entities={entityCoverage}",
            $"serializer_component_present={(component is not null ? "true" : "false")}"
        };
        if (component is not null)
        {
            evidenceNotes.Add($"managed_type_shape:{component.TypeHint.ManagedTypeShape}");
            evidenceNotes.Add($"buffer_element:{component.TypeHint.ImplementsBufferElementData.ToString().ToLowerInvariant()}");
            evidenceNotes.Add($"serializer_type:{component.SerializerType}");
        }

        return new CompanyResourceHypothesisResultFact(
            hypothesisKey,
            typeName,
            validationStatus,
            summary,
            new ReadOnlyCollection<string>(evidenceNotes));
    }

    private static CompanyResourceHypothesisResultFact BuildComponentCarrierResult(
        string hypothesisKey,
        string typeName,
        int entityCoverage,
        IReadOnlyCollection<SerializerCatalogComponentFact> serializerComponents,
        ManagedReconNotesFacts managedReconNotesFacts)
    {
        var note = managedReconNotesFacts.TypeNotes.First(item => string.Equals(item.TypeName, typeName, StringComparison.Ordinal));
        var component = FindComponent(serializerComponents, typeName);
        var isConfirmed = component is not null &&
                          string.Equals(component.TypeHint.ManagedTypeShape, "value_type", StringComparison.Ordinal) &&
                          entityCoverage > 0;
        var validationStatus = isConfirmed
            ? "confirmed"
            : component is not null || entityCoverage > 0
                ? "inconclusive"
                : "disproved";
        var summary = validationStatus switch
        {
            "confirmed" => $"{typeName} is a real save-backed component carrier with live entity coverage.",
            "inconclusive" => $"{typeName} is present in managed or save-side evidence, but the current sample does not fully confirm a useful save carrier.",
            _ => $"{typeName} did not produce enough managed/save overlap to support the hypothesis."
        };
        var evidenceNotes = new List<string>
        {
            $"managed_observation:{note.ManagedObservation}",
            $"coverage:entities={entityCoverage}",
            $"serializer_component_present={(component is not null ? "true" : "false")}"
        };
        if (component is not null)
        {
            evidenceNotes.Add($"managed_type_shape:{component.TypeHint.ManagedTypeShape}");
            evidenceNotes.Add($"buffer_element:{component.TypeHint.ImplementsBufferElementData.ToString().ToLowerInvariant()}");
            evidenceNotes.Add($"serializer_type:{component.SerializerType}");
        }

        return new CompanyResourceHypothesisResultFact(
            hypothesisKey,
            typeName,
            validationStatus,
            summary,
            new ReadOnlyCollection<string>(evidenceNotes));
    }

    private static CompanyResourceHypothesisResultFact BuildTagClueResult(
        string hypothesisKey,
        string typeName,
        int entityCoverage,
        IReadOnlyCollection<SerializerCatalogComponentFact> serializerComponents,
        ManagedReconNotesFacts managedReconNotesFacts)
    {
        var note = managedReconNotesFacts.TypeNotes.First(item => string.Equals(item.TypeName, typeName, StringComparison.Ordinal));
        var component = FindComponent(serializerComponents, typeName);
        var looksTagLike = note.ManagedObservation.Contains("tag-style", StringComparison.OrdinalIgnoreCase);
        var isConfirmed = component is not null &&
                          looksTagLike &&
                          entityCoverage > 0;
        var validationStatus = isConfirmed
            ? "confirmed"
            : component is not null || entityCoverage > 0
                ? "inconclusive"
                : "disproved";
        var summary = validationStatus switch
        {
            "confirmed" => $"{typeName} is present as a live companion clue, but the managed type still looks tag-style rather than a useful payload carrier.",
            "inconclusive" => $"{typeName} appears in managed or save-side evidence, but the current sample does not fully confirm whether it is only a companion clue.",
            _ => $"{typeName} did not produce enough managed/save overlap to support even a tag-clue interpretation."
        };
        var evidenceNotes = new List<string>
        {
            $"managed_observation:{note.ManagedObservation}",
            $"coverage:entities={entityCoverage}",
            $"serializer_component_present={(component is not null ? "true" : "false")}"
        };
        if (component is not null)
        {
            evidenceNotes.Add($"managed_type_shape:{component.TypeHint.ManagedTypeShape}");
            evidenceNotes.Add($"buffer_element:{component.TypeHint.ImplementsBufferElementData.ToString().ToLowerInvariant()}");
            evidenceNotes.Add($"serializer_type:{component.SerializerType}");
        }

        return new CompanyResourceHypothesisResultFact(
            hypothesisKey,
            typeName,
            validationStatus,
            summary,
            new ReadOnlyCollection<string>(evidenceNotes));
    }

    private static SerializerCatalogComponentFact? FindComponent(
        IReadOnlyCollection<SerializerCatalogComponentFact> serializerComponents,
        string typeName)
    {
        return serializerComponents.FirstOrDefault(
            component =>
                string.Equals(component.TypeName, typeName, StringComparison.Ordinal) ||
                component.TypeName.StartsWith(typeName + ",", StringComparison.Ordinal));
    }
}

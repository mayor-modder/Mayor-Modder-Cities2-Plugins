using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class PrimitiveComponentFactsExtractor
{
    private const int SampleLimitPerVariant = 8;

    public static PrimitiveComponentFacts Extract(
        byte[] payload,
        SavePreludeSummary summary,
        SerializerCatalogFacts serializerCatalogFacts)
    {
        var catalogByComponentIndex = serializerCatalogFacts.Components.ToDictionary(component => component.ComponentIndex);
        var samplesByKey = new Dictionary<(int ComponentIndex, PrimitiveDecoderKeyFact Key), List<PrimitiveComponentSampleFact>>();

        ArchetypeBufferWalker.Walk(
            payload,
            summary,
            onComponentBlock: context =>
            {
                if (!catalogByComponentIndex.TryGetValue(context.ComponentIndex, out var catalogComponent))
                {
                    return;
                }

                var decoderKey = GenericComponentDecoderRegistry.CreateKey(
                    context.ComponentType.SerializerType,
                    context.BytesPerEntity,
                    catalogComponent.TypeHint.ManagedTypeShape);
                if (!string.Equals(
                        GenericComponentDecoderRegistry.ResolveDecoderKind(decoderKey),
                        "fixed_width_value",
                        StringComparison.Ordinal))
                {
                    return;
                }

                var mapKey = (context.ComponentIndex, decoderKey);
                if (!samplesByKey.TryGetValue(mapKey, out var samples))
                {
                    samples = [];
                    samplesByKey[mapKey] = samples;
                }

                var remainingSampleCapacity = SampleLimitPerVariant - samples.Count;
                if (remainingSampleCapacity <= 0)
                {
                    return;
                }

                samples.AddRange(
                    GenericComponentDecoder.DecodeSamples(
                            context.Block,
                            context.ArchetypeContext.Archetype.EntityCount,
                            context.ArchetypeContext.EntityIndexBase,
                            context.ArchetypeContext.Archetype.Index,
                            remainingSampleCapacity)
                        .ToList());
            });

        var components = serializerCatalogFacts.Components
            .OrderBy(component => component.ComponentIndex)
            .Select(component => BuildComponentFact(component, samplesByKey))
            .ToList();

        return new PrimitiveComponentFacts(new ReadOnlyCollection<PrimitiveComponentFact>(components));
    }

    private static PrimitiveComponentFact BuildComponentFact(
        SerializerCatalogComponentFact component,
        IReadOnlyDictionary<(int ComponentIndex, PrimitiveDecoderKeyFact Key), List<PrimitiveComponentSampleFact>> samplesByKey)
    {
        var decoderKeys = component.BlockShapes.Count == 0
            ? [GenericComponentDecoderRegistry.CreateKey(component.SerializerType, null, component.TypeHint.ManagedTypeShape)]
            : component.BlockShapes
                .Select(blockShape => GenericComponentDecoderRegistry.CreateKey(
                    component.SerializerType,
                    blockShape.BytesPerEntity,
                    component.TypeHint.ManagedTypeShape))
                .Distinct()
                .ToList();

        var variants = decoderKeys
            .Select(
                decoderKey =>
                {
                    samplesByKey.TryGetValue((component.ComponentIndex, decoderKey), out var samples);
                    return new PrimitiveDecoderVariantFact(
                        decoderKey,
                        GenericComponentDecoderRegistry.ResolveDecoderKind(decoderKey),
                        new ReadOnlyCollection<PrimitiveComponentSampleFact>(samples ?? []));
                })
            .ToList();

        return new PrimitiveComponentFact(
            component.ComponentIndex,
            component.TypeName,
            component.EntityCoverage,
            component.ArchetypeCount,
            new ReadOnlyCollection<PrimitiveDecoderVariantFact>(variants));
    }
}

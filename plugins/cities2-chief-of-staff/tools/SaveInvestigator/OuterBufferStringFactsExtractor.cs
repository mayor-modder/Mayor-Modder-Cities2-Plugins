using System.Collections.ObjectModel;
namespace SaveInvestigator;

public static class OuterBufferStringFactsExtractor
{
    private const int MinimumStringLength = 6;

    public static OuterBufferStringFacts Extract(
        byte[] payload,
        SavePreludeSummary summary,
        RailTransitStationFacts railTransitStationFacts,
        TransportStationGraphFacts transportStationGraphFacts)
    {
        var targetTrainStopArchetypes = railTransitStationFacts.StopOwners
            .Where(stop => string.Equals(stop.Mode, "train", StringComparison.Ordinal))
            .Select(stop => stop.StopArchetypeIndex)
            .Where(index => index >= 0)
            .ToHashSet();
        var targetTrainStationArchetypes = transportStationGraphFacts.Stations
            .Where(station => string.Equals(station.Mode, "train", StringComparison.Ordinal))
            .Select(station => station.ArchetypeIndex)
            .Concat(
                transportStationGraphFacts.TrainStopOwnerChains
                    .SelectMany(chain => chain.Chain)
                    .Select(node => node.ArchetypeIndex))
            .Where(index => index >= 0)
            .ToHashSet();
        var namedRailStationArchetypes = railTransitStationFacts.Stations
            .Select(station => TryFindArchetype(summary.Archetypes, station.NameEntityIndex))
            .Where(match => match is not null)
            .Select(match => match!.Value.Archetype.Index)
            .ToHashSet();
        var targetArchetypes = targetTrainStopArchetypes
            .Concat(targetTrainStationArchetypes)
            .Concat(namedRailStationArchetypes)
            .ToHashSet();

        var sources = new List<OuterBufferStringSourceFact>();
        var cursor = ArchetypeBufferWalker.Walk(
            payload,
            summary,
            onArchetypeStarted: context =>
            {
                if (!targetArchetypes.Contains(context.Archetype.Index))
                {
                    return;
                }

                var componentTypes = context.Archetype.ComponentTypeIndexes
                    .Select(index => summary.ComponentTypes[index].TypeName)
                    .ToList();
                sources.Add(
                    new OuterBufferStringSourceFact(
                        "archetype",
                        context.Archetype.Index,
                        $"Archetype {context.Archetype.Index}",
                        context.ArchetypeBuffer.Length,
                        context.Archetype.EntityCount,
                        targetTrainStationArchetypes.Contains(context.Archetype.Index),
                        targetTrainStopArchetypes.Contains(context.Archetype.Index),
                        new ReadOnlyCollection<string>(componentTypes),
                        new ReadOnlyCollection<OuterBufferStringFact>(ExtractStrings(context.ArchetypeBuffer))));
            });

        for (var systemIndex = 0; systemIndex < summary.SystemTypes.Count; systemIndex += 1)
        {
            var buffer = cursor.ReadNextOuterBufferWithMetadata(summary.BufferFormat);
            var systemType = summary.SystemTypes[systemIndex];
            sources.Add(
                new OuterBufferStringSourceFact(
                    "system",
                    systemIndex,
                    systemType.TypeName,
                    buffer.UncompressedSize,
                    null,
                    false,
                    false,
                    new ReadOnlyCollection<string>([]),
                    new ReadOnlyCollection<OuterBufferStringFact>(ExtractStrings(buffer.Data))));
        }

        return new OuterBufferStringFacts(
            new ReadOnlyCollection<OuterBufferStringSourceFact>(
                sources
                    .Where(source => source.SourceKind == "archetype" || source.Strings.Count > 0)
                    .OrderBy(source => source.SourceKind, StringComparer.Ordinal)
                    .ThenBy(source => source.SourceIndex)
                    .ToList()));
    }

    private static List<OuterBufferStringFact> ExtractStrings(byte[] buffer)
    {
        return ReadableStringScanner.Scan(buffer, MinimumStringLength)
            .Select(item => new OuterBufferStringFact(item.Encoding, item.Value, item.Offset))
            .ToList();
    }

    private static (ArchetypeSummary Archetype, int EntityOrdinal)? TryFindArchetype(
        IReadOnlyList<ArchetypeSummary> archetypes,
        int entityIndex)
    {
        var entityIndexBase = 0;
        foreach (var archetype in archetypes)
        {
            var next = entityIndexBase + archetype.EntityCount;
            if (entityIndex >= entityIndexBase && entityIndex < next)
            {
                return (archetype, entityIndex - entityIndexBase);
            }

            entityIndexBase = next;
        }

        return null;
    }
}

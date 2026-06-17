using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class EntityBacklinkFactsExtractor
{
    public static EntityBacklinkFacts Extract(
        byte[] payload,
        SavePreludeSummary summary,
        TransportStationGraphFacts transportStationGraphFacts)
    {
        var targets = transportStationGraphFacts.Stations
            .Where(station => string.Equals(station.Mode, "train", StringComparison.Ordinal))
            .Select(station => new EntityBacklinkTargetFact(station.StationEntityIndex, station.ArchetypeIndex))
            .OrderBy(target => target.TargetEntityIndex)
            .ToList();
        var targetSet = targets
            .Select(target => target.TargetEntityIndex)
            .ToHashSet();

        if (targets.Count == 0)
        {
            return new EntityBacklinkFacts(
                new ReadOnlyCollection<EntityBacklinkTargetFact>([]),
                new ReadOnlyCollection<EntityBacklinkMatchFact>([]));
        }

        var matches = new List<EntityBacklinkMatchFact>();
        ArchetypeBufferWalker.Walk(
            payload,
            summary,
            onComponentBlock: context =>
            {
                var bytesPerEntity = context.BytesPerEntity;
                if (bytesPerEntity is null || bytesPerEntity < sizeof(int) || bytesPerEntity % sizeof(int) != 0)
                {
                    return;
                }

                for (var entityOrdinal = 0; entityOrdinal < context.ArchetypeContext.Archetype.EntityCount; entityOrdinal += 1)
                {
                    var sourceEntityIndex = context.ArchetypeContext.EntityIndexBase + entityOrdinal;
                    var entityOffset = entityOrdinal * bytesPerEntity.Value;
                    for (var fieldOffset = 0; fieldOffset <= bytesPerEntity.Value - sizeof(int); fieldOffset += sizeof(int))
                    {
                        var value = BitConverter.ToInt32(context.Block, entityOffset + fieldOffset);
                        if (!targetSet.Contains(value))
                        {
                            continue;
                        }

                        matches.Add(
                            new EntityBacklinkMatchFact(
                                value,
                                sourceEntityIndex,
                                context.ArchetypeContext.Archetype.Index,
                                entityOrdinal,
                                context.ComponentIndex,
                                context.ComponentOrdinal,
                                context.ComponentType.TypeName,
                                context.ComponentType.SerializerType,
                                bytesPerEntity.Value,
                                fieldOffset));
                    }
                }
            });

        return new EntityBacklinkFacts(
            new ReadOnlyCollection<EntityBacklinkTargetFact>(targets),
            new ReadOnlyCollection<EntityBacklinkMatchFact>(
                matches
                    .OrderBy(match => match.TargetEntityIndex)
                    .ThenBy(match => match.SourceEntityIndex)
                    .ThenBy(match => match.SourceComponentOrdinal)
                    .ThenBy(match => match.FieldOffset)
                    .ToList()));
    }
}

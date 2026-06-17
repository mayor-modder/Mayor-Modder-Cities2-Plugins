namespace SaveInvestigator;

public static class ArchetypeBufferWalker
{
    // Current saves treat serializer types where `(serializerType & 0x11) == 0x01` as payloadless/tag-style entries.
    private const byte PayloadlessSerializerMask = 17;
    private const byte PayloadlessSerializerValue = 1;

    public static SaveGameDataCursor Walk(
        byte[] payload,
        SavePreludeSummary summary,
        Action<ArchetypeBufferWalkerContext>? onArchetypeStarted = null,
        Action<ArchetypeComponentBlockContext>? onComponentBlock = null,
        Action<ArchetypeBufferWalkerContext>? onArchetypeCompleted = null)
    {
        var outerCursor = SaveGameDataCursor.AdvanceToArchetypeBuffers(payload, summary.BufferFormat);
        var entityIndexBase = 0;

        foreach (var archetype in summary.Archetypes)
        {
            var buffer = outerCursor.ReadNextOuterBuffer(summary.BufferFormat);
            var archetypeContext = new ArchetypeBufferWalkerContext(summary, archetype, entityIndexBase, buffer);
            onArchetypeStarted?.Invoke(archetypeContext);

            var archetypeCursor = new SaveGameDataCursor(buffer);
            for (var componentOrdinal = 0; componentOrdinal < archetype.ComponentTypeIndexes.Count; componentOrdinal += 1)
            {
                var componentIndex = archetype.ComponentTypeIndexes[componentOrdinal];
                var componentType = summary.ComponentTypes[componentIndex];
                if (IsPayloadlessSerializer(componentType.SerializerType))
                {
                    continue;
                }

                var block = archetypeCursor.ReadBlock();
                onComponentBlock?.Invoke(
                    new ArchetypeComponentBlockContext(
                        archetypeContext,
                        componentOrdinal,
                        componentIndex,
                        componentType,
                        block,
                        TryGetBytesPerEntity(block.Length, archetype.EntityCount)));
            }

            onArchetypeCompleted?.Invoke(archetypeContext);
            entityIndexBase += archetype.EntityCount;
        }

        return outerCursor;
    }

    public static bool IsPayloadlessSerializer(byte serializerType)
    {
        return (serializerType & PayloadlessSerializerMask) == PayloadlessSerializerValue;
    }

    public static int? TryGetBytesPerEntity(int blockLength, int entityCount)
    {
        if (entityCount <= 0 || blockLength < 0 || blockLength % entityCount != 0)
        {
            return null;
        }

        return blockLength / entityCount;
    }
}

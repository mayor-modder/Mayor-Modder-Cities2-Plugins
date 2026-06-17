using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class TransportLineParser
{
    public static ReadOnlyCollection<TransportLineParseResult> ParseArchetype(
        int entityIndexBase,
        int archetypeIndex,
        int entityCount,
        byte[]? transportLineBlock,
        byte[]? routeNumberBlock,
        byte[]? routeColorBlock,
        byte[]? vehicleModelBlock = null)
    {
        if (transportLineBlock is null || routeNumberBlock is null || routeColorBlock is null)
        {
            return new ReadOnlyCollection<TransportLineParseResult>([]);
        }

        var partialLines = ParseTransportLines(transportLineBlock, entityCount);
        ParseRouteNumbers(routeNumberBlock, partialLines);
        ParseRouteColors(routeColorBlock, partialLines);
        ParseVehicleModels(vehicleModelBlock, partialLines);

        return new ReadOnlyCollection<TransportLineParseResult>(
            partialLines
                .Where(line => line.RouteNumber.HasValue)
                .Select(
                    (line, entityOrdinal) => new TransportLineParseResult(
                        entityIndexBase + entityOrdinal,
                        archetypeIndex,
                        entityOrdinal,
                        line.RouteNumber ?? 0,
                        line.ColorHex,
                        line.VehicleInterval,
                        line.UnbunchingFactor,
                        line.Flags,
                        line.TicketPrice,
                        line.VehicleRequestEntityIndex,
                        line.VehiclePrefabEntityIndex))
                .ToList());
    }

    private static List<TransportLinePartial> ParseTransportLines(byte[] block, int entityCount)
    {
        var cursor = new SaveGameDataCursor(block);
        var partialLines = new List<TransportLinePartial>(entityCount);
        for (var entityOrdinal = 0; entityOrdinal < entityCount; entityOrdinal += 1)
        {
            partialLines.Add(
                new TransportLinePartial(
                    cursor.ReadInt32(),
                    cursor.ReadSingle(),
                    cursor.ReadSingle(),
                    cursor.ReadUInt16(),
                    cursor.ReadUInt16()));
        }

        return partialLines;
    }

    private static void ParseRouteNumbers(byte[] block, IList<TransportLinePartial> partialLines)
    {
        var routeNumbers = new int[partialLines.Count];
        if (!GenericComponentDecoder.TryDecodeInt32Lane(block, partialLines.Count, routeNumbers))
        {
            throw new InvalidOperationException("Route number block did not match the expected fixed-width int32 layout.");
        }

        for (var entityOrdinal = 0; entityOrdinal < partialLines.Count; entityOrdinal += 1)
        {
            partialLines[entityOrdinal] = partialLines[entityOrdinal] with
            {
                RouteNumber = routeNumbers[entityOrdinal]
            };
        }
    }

    private static void ParseRouteColors(byte[] block, IList<TransportLinePartial> partialLines)
    {
        var cursor = new SaveGameDataCursor(block);
        for (var entityOrdinal = 0; entityOrdinal < partialLines.Count; entityOrdinal += 1)
        {
            var red = cursor.ReadByte();
            var green = cursor.ReadByte();
            var blue = cursor.ReadByte();
            cursor.ReadByte();

            partialLines[entityOrdinal] = partialLines[entityOrdinal] with
            {
                ColorHex = $"#{red:X2}{green:X2}{blue:X2}"
            };
        }
    }

    private static void ParseVehicleModels(byte[]? block, IList<TransportLinePartial> partialLines)
    {
        if (block is null)
        {
            return;
        }

        var bytesPerEntity = ArchetypeBufferWalker.TryGetBytesPerEntity(block.Length, partialLines.Count);
        if (!bytesPerEntity.HasValue || bytesPerEntity.Value < sizeof(int) * 2)
        {
            return;
        }

        for (var entityOrdinal = 0; entityOrdinal < partialLines.Count; entityOrdinal += 1)
        {
            var offset = entityOrdinal * bytesPerEntity.Value;
            partialLines[entityOrdinal] = partialLines[entityOrdinal] with
            {
                VehiclePrefabEntityIndex = BitConverter.ToInt32(block, offset + sizeof(int))
            };
        }
    }

    private sealed record TransportLinePartial(
        int VehicleRequestEntityIndex,
        float VehicleInterval,
        float UnbunchingFactor,
        ushort Flags,
        ushort TicketPrice)
    {
        public int? RouteNumber { get; init; }

        public string ColorHex { get; init; } = "#000000";

        public int? VehiclePrefabEntityIndex { get; init; }
    }
}

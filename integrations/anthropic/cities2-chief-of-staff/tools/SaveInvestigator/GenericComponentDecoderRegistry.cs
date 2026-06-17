namespace SaveInvestigator;

public static class GenericComponentDecoderRegistry
{
    private const string PayloadlessBlockShape = "payloadless";
    private const string FixedWidthBlockShape = "fixed_width";
    private const string VariableWidthBlockShape = "variable_width";

    public static PrimitiveDecoderKeyFact CreateKey(
        byte serializerType,
        int? bytesPerEntity,
        string? managedTypeShape)
    {
        return new PrimitiveDecoderKeyFact(
            serializerType,
            NormalizeBlockShape(serializerType, bytesPerEntity),
            bytesPerEntity,
            string.IsNullOrWhiteSpace(managedTypeShape) ? "unknown" : managedTypeShape);
    }

    public static string ResolveDecoderKind(PrimitiveDecoderKeyFact key)
    {
        return key.BlockShape switch
        {
            PayloadlessBlockShape => "tag",
            FixedWidthBlockShape => "fixed_width_value",
            _ => "unsupported"
        };
    }

    private static string NormalizeBlockShape(byte serializerType, int? bytesPerEntity)
    {
        if (ArchetypeBufferWalker.IsPayloadlessSerializer(serializerType))
        {
            return PayloadlessBlockShape;
        }

        return bytesPerEntity.HasValue ? FixedWidthBlockShape : VariableWidthBlockShape;
    }
}

namespace SaveInvestigator;

public sealed record ArchetypeBufferWalkerContext(
    SavePreludeSummary Summary,
    ArchetypeSummary Archetype,
    int EntityIndexBase,
    byte[] ArchetypeBuffer);

public sealed record ArchetypeComponentBlockContext(
    ArchetypeBufferWalkerContext ArchetypeContext,
    int ComponentOrdinal,
    int ComponentIndex,
    ComponentTypeSummary ComponentType,
    byte[] Block,
    int? BytesPerEntity)
{
    public int BlockLength => Block.Length;
}

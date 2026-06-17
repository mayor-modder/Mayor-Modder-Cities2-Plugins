namespace SaveInvestigator;

public static class SerializedTypeIndexLookup
{
    public static int FindComponentIndex(IReadOnlyList<ComponentTypeSummary> componentTypes, string requiredPrefix)
    {
        var match = componentTypes.FirstOrDefault(
            component => component.TypeName.StartsWith(requiredPrefix, StringComparison.Ordinal));
        return match?.Index ?? -1;
    }
}

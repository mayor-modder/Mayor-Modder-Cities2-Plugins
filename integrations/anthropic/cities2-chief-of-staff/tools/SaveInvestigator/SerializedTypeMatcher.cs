namespace SaveInvestigator;

public static class SerializedTypeMatcher
{
    public static bool Contains(IReadOnlySet<string> serializedTypeNames, string typeName)
    {
        return serializedTypeNames.Any(serializedTypeName => Matches(serializedTypeName, typeName));
    }

    public static bool Matches(string serializedTypeName, string typeName)
    {
        return string.Equals(serializedTypeName, typeName, StringComparison.Ordinal) ||
               serializedTypeName.StartsWith(typeName + ",", StringComparison.Ordinal);
    }
}

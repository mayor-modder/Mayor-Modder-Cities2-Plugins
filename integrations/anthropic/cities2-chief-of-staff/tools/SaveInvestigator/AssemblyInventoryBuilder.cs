using System.Collections.ObjectModel;
using System.Reflection;

namespace SaveInvestigator;

public static class AssemblyInventoryBuilder
{
    private static readonly string[] CandidateNamespacePrefixes =
    [
        "Game.Serialization",
        "Colossal.Serialization.Entities",
        "Game.Assets"
    ];

    public static (ReadOnlyCollection<AssemblyInventoryItem> assemblies, ReadOnlyCollection<CandidateTypeInventoryItem> candidateTypes)
        Build(string managedPath)
    {
        var gameAssemblyPaths = Directory.EnumerateFiles(managedPath, "*.dll")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var resolver = new PathAssemblyResolver(gameAssemblyPaths);
        var assemblyInventory = new List<AssemblyInventoryItem>();
        var candidateTypes = new List<CandidateTypeInventoryItem>();

        using var metadataLoadContext = new MetadataLoadContext(resolver, "mscorlib");
        foreach (var assemblyPath in gameAssemblyPaths)
        {
            try
            {
                var assembly = metadataLoadContext.LoadFromAssemblyPath(assemblyPath);
                var matchingTypes = assembly.GetTypes()
                    .Where(type => IsCandidateType(type))
                    .OrderBy(type => type.FullName, StringComparer.Ordinal)
                    .ToArray();

                assemblyInventory.Add(new AssemblyInventoryItem(
                    assembly.GetName().Name ?? Path.GetFileNameWithoutExtension(assemblyPath),
                    assemblyPath,
                    matchingTypes.Length));

                foreach (var type in matchingTypes)
                {
                    if (type.FullName is null)
                    {
                        continue;
                    }

                    candidateTypes.Add(new CandidateTypeInventoryItem(
                        assembly.GetName().Name ?? Path.GetFileNameWithoutExtension(assemblyPath),
                        type.FullName));
                }
            }
            catch (Exception)
            {
                assemblyInventory.Add(new AssemblyInventoryItem(
                    Path.GetFileNameWithoutExtension(assemblyPath),
                    assemblyPath,
                    -1));
            }
        }

        return (
            new ReadOnlyCollection<AssemblyInventoryItem>(assemblyInventory),
            new ReadOnlyCollection<CandidateTypeInventoryItem>(candidateTypes));
    }

    public static ReadOnlyCollection<TypeResolutionSummary> ResolveSerializedTypes(
        string managedPath,
        IEnumerable<string> typeNames)
    {
        var gameAssemblyPaths = Directory.EnumerateFiles(managedPath, "*.dll")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var resolver = new PathAssemblyResolver(gameAssemblyPaths);
        var assemblyPathByName = gameAssemblyPaths.ToDictionary(
            path => Path.GetFileNameWithoutExtension(path),
            path => path,
            StringComparer.OrdinalIgnoreCase);
        var results = new List<TypeResolutionSummary>();

        using var metadataLoadContext = new MetadataLoadContext(resolver, "mscorlib");
        foreach (var typeName in typeNames.Distinct(StringComparer.Ordinal))
        {
            var resolved = TryResolveType(metadataLoadContext, assemblyPathByName, typeName);
            results.Add(resolved);
        }

        return new ReadOnlyCollection<TypeResolutionSummary>(
            results.OrderBy(result => result.TypeName, StringComparer.Ordinal).ToList());
    }

    private static bool IsCandidateType(Type type)
    {
        if (type.FullName is null)
        {
            return false;
        }

        return CandidateNamespacePrefixes.Any(prefix => type.FullName.StartsWith(prefix, StringComparison.Ordinal)) ||
               type.FullName.Contains("SaveGame", StringComparison.Ordinal);
    }

    private static TypeResolutionSummary TryResolveType(
        MetadataLoadContext metadataLoadContext,
        IReadOnlyDictionary<string, string> assemblyPathByName,
        string serializedTypeName)
    {
        var splitIndex = serializedTypeName.IndexOf(',');
        if (splitIndex <= 0)
        {
            return new TypeResolutionSummary(serializedTypeName, false, null, null);
        }

        var fullTypeName = serializedTypeName[..splitIndex].Trim();
        var assemblySimpleName = serializedTypeName[(splitIndex + 1)..].Trim();
        try
        {
            var assembly = metadataLoadContext.LoadFromAssemblyName(new AssemblyName(assemblySimpleName));
            var type = assembly.GetType(fullTypeName, throwOnError: false, ignoreCase: false);
            if (type is null)
            {
                return new TypeResolutionSummary(serializedTypeName, false, null, null);
            }

            assemblyPathByName.TryGetValue(assembly.GetName().Name ?? assemblySimpleName, out var resolvedPath);
            return new TypeResolutionSummary(
                serializedTypeName,
                true,
                resolvedPath,
                assembly.GetName().Name);
        }
        catch
        {
            return new TypeResolutionSummary(serializedTypeName, false, null, null);
        }
    }
}

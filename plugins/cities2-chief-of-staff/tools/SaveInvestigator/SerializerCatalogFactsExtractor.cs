using System.Collections.ObjectModel;
using System.Reflection;

namespace SaveInvestigator;

public static class SerializerCatalogFactsExtractor
{
    private const string BufferElementInterfaceName = "Unity.Entities.IBufferElementData";

    public static SerializerCatalogFacts Extract(byte[] payload, SavePreludeSummary summary, string managedPath)
    {
        var typeHints = ResolveTypeHints(managedPath, summary.ComponentTypes.Select(component => component.TypeName));
        return Extract(
            payload,
            summary,
            serializedTypeName => typeHints.TryGetValue(serializedTypeName, out var hint)
                ? hint
                : new SerializerCatalogTypeHint(false, null, null, "unknown", false));
    }

    public static SerializerCatalogFacts Extract(
        byte[] payload,
        SavePreludeSummary summary,
        Func<string, SerializerCatalogTypeHint> resolveTypeHint)
    {
        var coverageByComponentIndex = summary.Archetypes
            .SelectMany(
                archetype => archetype.ComponentTypeIndexes.Select(
                    componentIndex => new
                    {
                        ComponentIndex = componentIndex,
                        archetype.EntityCount
                    }))
            .GroupBy(item => item.ComponentIndex)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    EntityCoverage = group.Sum(item => item.EntityCount),
                    ArchetypeCount = group.Count()
                });

        var blockShapesByComponentIndex = new Dictionary<int, List<SerializerCatalogBlockShapeFact>>();
        ArchetypeBufferWalker.Walk(
            payload,
            summary,
            onComponentBlock: context =>
            {
                if (!blockShapesByComponentIndex.TryGetValue(context.ComponentIndex, out var blockShapes))
                {
                    blockShapes = [];
                    blockShapesByComponentIndex[context.ComponentIndex] = blockShapes;
                }

                blockShapes.Add(
                    new SerializerCatalogBlockShapeFact(
                        context.ArchetypeContext.Archetype.Index,
                        context.ArchetypeContext.Archetype.EntityCount,
                        context.BlockLength,
                        context.BytesPerEntity));
            });

        var components = summary.ComponentTypes
            .Select(component =>
            {
                coverageByComponentIndex.TryGetValue(component.Index, out var coverage);
                blockShapesByComponentIndex.TryGetValue(component.Index, out var blockShapes);
                return new SerializerCatalogComponentFact(
                    component.Index,
                    component.TypeName,
                    component.SerializerType,
                    coverage?.EntityCoverage ?? 0,
                    coverage?.ArchetypeCount ?? 0,
                    new ReadOnlyCollection<SerializerCatalogBlockShapeFact>(
                        (blockShapes ?? [])
                            .OrderBy(shape => shape.ArchetypeIndex)
                            .ToList()),
                    resolveTypeHint(component.TypeName));
            })
            .OrderBy(component => component.ComponentIndex)
            .ToList();

        return new SerializerCatalogFacts(
            new ReadOnlyCollection<SerializerCatalogComponentFact>(components));
    }
    private static Dictionary<string, SerializerCatalogTypeHint> ResolveTypeHints(
        string managedPath,
        IEnumerable<string> serializedTypeNames)
    {
        var gameAssemblyPaths = Directory.EnumerateFiles(managedPath, "*.dll")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var resolver = new PathAssemblyResolver(gameAssemblyPaths);
        var assemblyPathByName = gameAssemblyPaths.ToDictionary(
            path => Path.GetFileNameWithoutExtension(path),
            path => path,
            StringComparer.OrdinalIgnoreCase);
        var results = new Dictionary<string, SerializerCatalogTypeHint>(StringComparer.Ordinal);

        using var metadataLoadContext = new MetadataLoadContext(resolver, "mscorlib");
        foreach (var serializedTypeName in serializedTypeNames.Distinct(StringComparer.Ordinal))
        {
            results[serializedTypeName] = ResolveTypeHint(metadataLoadContext, assemblyPathByName, serializedTypeName);
        }

        return results;
    }

    private static SerializerCatalogTypeHint ResolveTypeHint(
        MetadataLoadContext metadataLoadContext,
        IReadOnlyDictionary<string, string> assemblyPathByName,
        string serializedTypeName)
    {
        var splitIndex = serializedTypeName.IndexOf(',');
        if (splitIndex <= 0)
        {
            return new SerializerCatalogTypeHint(false, null, null, "unknown", false);
        }

        var fullTypeName = serializedTypeName[..splitIndex].Trim();
        var assemblySimpleName = serializedTypeName[(splitIndex + 1)..].Trim();
        try
        {
            var assembly = metadataLoadContext.LoadFromAssemblyName(new AssemblyName(assemblySimpleName));
            var type = assembly.GetType(fullTypeName, throwOnError: false, ignoreCase: false);
            if (type is null)
            {
                return new SerializerCatalogTypeHint(false, null, null, "unknown", false);
            }

            assemblyPathByName.TryGetValue(assembly.GetName().Name ?? assemblySimpleName, out var resolvedPath);
            var implementsBufferElementData = type.GetInterfaces()
                .Any(@interface => string.Equals(@interface.FullName, BufferElementInterfaceName, StringComparison.Ordinal));
            var managedTypeShape = implementsBufferElementData
                ? "buffer_element"
                : type.IsValueType
                    ? "value_type"
                    : type.IsClass
                        ? "reference_type"
                        : "other";

            return new SerializerCatalogTypeHint(
                true,
                assembly.GetName().Name,
                resolvedPath,
                managedTypeShape,
                implementsBufferElementData);
        }
        catch (Exception)
        {
            return new SerializerCatalogTypeHint(false, null, null, "unknown", false);
        }
    }
}

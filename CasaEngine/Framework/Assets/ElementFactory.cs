using System.Reflection;
using System.Runtime.Loader;
using CasaEngine.Core.Logging;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Common;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets;

public static class ElementFactory
{
    private static Dictionary<string, Type> _typeCache;

    // Gameplay script assemblies live in a collectible AssemblyLoadContext and are
    // registered explicitly: the AppDomain scan skips collectible contexts so an
    // unloading script assembly can never be re-pinned by a cache rebuild.
    private static readonly List<Assembly> _scriptAssemblies = new();

    static ElementFactory()
    {
        AppDomain.CurrentDomain.AssemblyLoad += (_, _) => InvalidateComponentCaches();
    }

    public static T Create<T>(string typeName) where T : class
    {
        var type = FindTypeByName(typeName);
        return Activator.CreateInstance(type) as T;
    }

    public static T Load<T>(JObject element) where T : class, ISerializable
    {
        var typeName = element["type"].GetString();
        var component = Create<T>(typeName);
        component.Load(element);
        return component;
    }

    // ---- Script objects (ADR-0075) -------------------------------------------------------------------------
    // A script object is created from a type name written in the data (a gameplay proxy, a player controller,
    // a gameplay mode, a design-time view model). It is looked up by its exact full name, in the registered
    // script assemblies only -- never in the engine, never by simple name. Components keep Create / Load.

    private static ScriptTypeIndex _scriptTypeIndex;

    private sealed class ScriptTypeIndex
    {
        public readonly Dictionary<string, Type> ByFullName = new(StringComparer.Ordinal);
        public readonly Dictionary<string, List<Type>> Ambiguous = new(StringComparer.Ordinal);
    }

    /// <summary>
    /// Creates the script object named by <paramref name="typeName"/> (full name, exact case) from the registered
    /// script assemblies. The file named in an error is taken from <paramref name="source"/> when the name comes
    /// from a loaded asset. A name that does not resolve throws <see cref="ScriptTypeResolutionException"/>,
    /// except while the editor preview tolerance is open: the problem is then logged and null is returned.
    /// </summary>
    public static T CreateScriptObject<T>(string typeName, string key, ObjectBase source = null) where T : class
    {
        var failure = TryResolveScriptTypeCore(typeof(T), key, typeName, out var type);
        if (failure == null)
        {
            return (T)Activator.CreateInstance(type);
        }

        failure.CompleteFileName(ResolveSourceFileName(source));
        if (!ScriptTypeResolutionTolerance.IsActive)
        {
            throw failure;
        }

        failure.CompleteFileName(ScriptTypeResolutionTolerance.FallbackFileName);
        Logs.WriteWarning(failure.Message);
        return null;
    }

    /// <summary>
    /// Resolves a script type name without throwing and without creating anything. Returns true and the type when
    /// the name resolves; otherwise false and the message that <see cref="CreateScriptObject{T}"/> would carry.
    /// For the editor, which shows the message next to the script name.
    /// </summary>
    public static bool TryResolveScriptType(Type requiredBaseType, string key, string typeName, out Type type, out string message)
    {
        ArgumentNullException.ThrowIfNull(requiredBaseType);

        var failure = TryResolveScriptTypeCore(requiredBaseType, key, typeName, out type);
        message = failure?.Message ?? string.Empty;
        return failure == null;
    }

    /// <summary>
    /// The full names of the public top-level types derived from <paramref name="baseType"/> that the registered
    /// script assemblies provide (instantiable: concrete, with a public parameterless constructor), ordered.
    /// For the editor; allocates.
    /// </summary>
    public static List<string> GetScriptTypeNames(Type baseType)
    {
        ArgumentNullException.ThrowIfNull(baseType);

        var names = new List<string>();
        foreach (var pair in GetScriptTypeIndex().ByFullName)
        {
            if (baseType.IsAssignableFrom(pair.Value) && pair.Value != baseType)
            {
                names.Add(pair.Key);
            }
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    private static string ResolveSourceFileName(ObjectBase source)
    {
        if (source == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(source.FileName))
        {
            return source.FileName;
        }

        return source.AssetId != Guid.Empty ? AssetCatalog.Get(source.AssetId)?.FileName : null;
    }

    private static ScriptTypeIndex GetScriptTypeIndex()
    {
        // Local copy: Register/UnregisterScriptAssembly null the field while another thread resolves.
        var index = _scriptTypeIndex;
        if (index != null)
        {
            return index;
        }

        index = new ScriptTypeIndex();
        foreach (var assembly in _scriptAssemblies)
        {
            foreach (var candidate in assembly.GetTypes())
            {
                if (!IsInstantiableScriptType(candidate))
                {
                    continue;
                }

                var fullName = candidate.FullName;
                if (index.Ambiguous.TryGetValue(fullName, out var duplicates))
                {
                    duplicates.Add(candidate);
                }
                else if (index.ByFullName.TryGetValue(fullName, out var first))
                {
                    index.Ambiguous.Add(fullName, new List<Type> { first, candidate });
                }
                else
                {
                    index.ByFullName.Add(fullName, candidate);
                }
            }
        }

        _scriptTypeIndex = index;
        return index;
    }

    private static bool IsInstantiableScriptType(Type type) =>
        type is { IsClass: true, IsPublic: true, IsAbstract: false, IsGenericTypeDefinition: false }
        && type.GetConstructor(Type.EmptyTypes) != null;

    private static ScriptTypeResolutionException TryResolveScriptTypeCore(Type requiredBaseType, string key, string typeName, out Type type)
    {
        type = null;

        if (!string.IsNullOrWhiteSpace(typeName))
        {
            var index = GetScriptTypeIndex();
            if (index.Ambiguous.Count > 0 && index.Ambiguous.TryGetValue(typeName, out var duplicates))
            {
                return BuildAmbiguousFailure(key, typeName, duplicates);
            }

            if (index.ByFullName.TryGetValue(typeName, out var found))
            {
                if (requiredBaseType.IsAssignableFrom(found))
                {
                    type = found;
                    return null;
                }

                return new ScriptTypeResolutionException(key, typeName,
                    $"the type is found but is not a {requiredBaseType.FullName}.",
                    DescribeScriptAssemblies(), string.Empty);
            }
        }

        return BuildNotFoundFailure(key, typeName);
    }

    private static string DescribeScriptAssemblies()
    {
        if (_scriptAssemblies.Count == 0)
        {
            return "none (no gameplay assembly is registered)";
        }

        var names = new string[_scriptAssemblies.Count];
        for (int i = 0; i < names.Length; i++)
        {
            names[i] = _scriptAssemblies[i].GetName().Name;
        }

        return string.Join(", ", names);
    }

    private static ScriptTypeResolutionException BuildAmbiguousFailure(string key, string typeName, List<Type> duplicates)
    {
        var assemblies = new string[duplicates.Count];
        for (int i = 0; i < assemblies.Length; i++)
        {
            assemblies[i] = duplicates[i].Assembly.GetName().Name;
        }

        return new ScriptTypeResolutionException(key, typeName,
            $"the full name is ambiguous: it is defined by {duplicates.Count} registered assemblies ({string.Join(", ", assemblies)}).",
            DescribeScriptAssemblies(), string.Empty);
    }

    private static ScriptTypeResolutionException BuildNotFoundFailure(string key, string typeName)
    {
        var assemblies = DescribeScriptAssemblies();

        if (string.IsNullOrWhiteSpace(typeName))
        {
            return new ScriptTypeResolutionException(key, typeName, "the name is empty.", assemblies, string.Empty);
        }

        if (_scriptAssemblies.Count == 0)
        {
            return new ScriptTypeResolutionException(key, typeName,
                "no gameplay assembly is registered, so no script type can be created (is the project's gameplay assembly built and loaded?).",
                assemblies, string.Empty);
        }

        // Diagnosis, failure path only (allocations are fine here): look for the closest spelling.
        var shortName = typeName;
        var lastDot = typeName.LastIndexOf('.');
        if (lastDot >= 0 && lastDot < typeName.Length - 1)
        {
            shortName = typeName.Substring(lastDot + 1);
        }

        string sameSpellingOtherCase = null;
        string unacceptableShape = null;
        var sameShortName = new List<string>();
        foreach (var assembly in _scriptAssemblies)
        {
            foreach (var candidate in assembly.GetTypes())
            {
                var fullName = candidate.FullName;
                if (fullName == null)
                {
                    continue;
                }

                var dottedName = fullName.Replace('+', '.');
                var instantiable = IsInstantiableScriptType(candidate);
                if (string.Equals(dottedName, typeName, StringComparison.Ordinal) && !instantiable)
                {
                    unacceptableShape = fullName;
                }
                else if (instantiable && string.Equals(dottedName, typeName, StringComparison.OrdinalIgnoreCase))
                {
                    sameSpellingOtherCase = fullName;
                }
                else if (instantiable && string.Equals(candidate.Name, shortName, StringComparison.OrdinalIgnoreCase))
                {
                    sameShortName.Add(fullName);
                }
            }
        }

        if (unacceptableShape != null)
        {
            return new ScriptTypeResolutionException(key, typeName,
                $"the type '{unacceptableShape}' exists but is not a public, concrete, non-generic, top-level class with a public parameterless constructor.",
                assemblies, string.Empty);
        }

        var hint = string.Empty;
        if (sameSpellingOtherCase != null)
        {
            hint = $"Did you mean '{sameSpellingOtherCase}'? (full names are case-sensitive)";
        }
        else if (sameShortName.Count > 0)
        {
            sameShortName.Sort(StringComparer.Ordinal);
            hint = $"Did you mean '{string.Join("' or '", sameShortName)}'? (script types are named by full name, namespace included)";
        }

        return new ScriptTypeResolutionException(key, typeName,
            "no public top-level type has this full name in the registered script assemblies.",
            assemblies, hint);
    }

    /// <summary>
    /// Makes the types of a reloadable script assembly resolvable by name.
    /// The assembly stays pinned until <see cref="UnregisterScriptAssembly"/>.
    /// </summary>
    public static void RegisterScriptAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        if (!_scriptAssemblies.Contains(assembly))
        {
            _scriptAssemblies.Add(assembly);
        }

        InvalidateCaches();
    }

    /// <summary>
    /// Removes a script assembly and drops every cached Type so its collectible
    /// load context can actually be collected.
    /// </summary>
    public static void UnregisterScriptAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        _scriptAssemblies.Remove(assembly);
        InvalidateCaches();
    }

    /// <summary>Drops the cached Type maps; they rebuild lazily on next use.</summary>
    public static void InvalidateCaches()
    {
        InvalidateComponentCaches();
        _scriptTypeIndex = null;
    }

    // The component maps follow every AssemblyLoad; the script index follows only the registered script
    // assemblies, so it never rebuilds (nor pins a collectible context) for an unrelated load.
    private static void InvalidateComponentCaches()
    {
        _typeCache = null;
        _derivedTypesCache = new();
    }

    private static IEnumerable<Assembly> GetCandidateAssemblies()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (AssemblyLoadContext.GetLoadContext(assembly)?.IsCollectible == true)
            {
                continue;
            }

            yield return assembly;
        }

        foreach (var assembly in _scriptAssemblies)
        {
            yield return assembly;
        }
    }

    private static Dictionary<string, Type> BuildTypeCache() =>
        GetCandidateAssemblies()
            .SelectMany(x => x.GetTypes())
            .GroupBy(x => x.Name, StringComparer.InvariantCultureIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.InvariantCultureIgnoreCase);

    private static Type FindTypeByName(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return null;
        }

        // Local copy: InvalidateCaches can null the field from an AssemblyLoad event
        // while another thread resolves a type.
        var cache = _typeCache;
        if (cache == null)
        {
            cache = BuildTypeCache();
            _typeCache = cache;
        }

        cache.TryGetValue(typeName, out var type);
        return type;
    }

    private static Dictionary<Type, IEnumerable<Type>> _derivedTypesCache = new();

    private static IEnumerable<Type> BuildDerivedTypes(Type type) =>
        GetCandidateAssemblies()
            .SelectMany(x => x.GetTypes())
            .Where(x => x is { IsClass: true, IsGenericType: false, IsInterface: false, IsAbstract: false }
                        && x.IsSubclassOf(type))
            .ToList();

    public static IEnumerable<Type> GetDerivedTypesFrom<T>() where T : class
    {
        var type = typeof(T);
        var cache = _derivedTypesCache;
        if (!cache.TryGetValue(type, out var derived))
        {
            derived = BuildDerivedTypes(type);
            cache[type] = derived;
        }
        return derived;
    }
}

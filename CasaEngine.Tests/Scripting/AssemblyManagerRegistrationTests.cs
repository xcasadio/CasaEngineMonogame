using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.EditorServices.Scripting;
using CasaEngine.Engine.Plugins;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Scripting;
using Xunit;

namespace CasaEngine.Tests.Scripting;

/// <summary>
/// One owner of the registration by path (ADR-0075, D5): <see cref="AssemblyManager.Load"/> registers the gameplay
/// assembly it loads with the default loader, and replaces its own registration when it reloads. With a loader the
/// caller provides (the editor's shadow copy, which registers and unregisters by itself) it registers nothing and
/// keeps no reference.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class AssemblyManagerRegistrationTests
{
    private const string ProxyFullName = "TestScripts.TestScriptProxy";

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "casa-assembly-manager-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ResolvedVersion()
    {
        var proxy = ElementFactory.CreateScriptObject<GameplayProxy>(ProxyFullName, "script_class_name");
        return (int)proxy.GetType().GetProperty("Version")!.GetValue(null)!;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsResolvable()
    {
        return ElementFactory.TryResolveScriptType(typeof(GameplayProxy), "script_class_name", ProxyFullName, out _, out _);
    }

    [Fact]
    public void DefaultLoader_RegistersTheGameplayAssembly()
    {
        string directory = CreateTempDirectory();
        try
        {
            string path = ScriptAssemblyCompiler.CompileScriptAssembly(directory, "ManagerDefaultLoad", version: 1);

            new AssemblyManager().Load(path);

            Assert.Equal(1, ResolvedVersion());
        }
        finally
        {
            ElementFactory.UnregisterScriptAssembly(Assembly.LoadFile(Path.Combine(directory, "ManagerDefaultLoad.dll")));
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void DefaultLoader_ReloadReplacesItsOwnRegistration()
    {
        string directory1 = CreateTempDirectory();
        string directory2 = CreateTempDirectory();
        var manager = new AssemblyManager();
        try
        {
            manager.Load(ScriptAssemblyCompiler.CompileScriptAssembly(directory1, "ManagerReloaded", version: 1));
            Assert.Equal(1, ResolvedVersion());

            // Both assemblies define the same full name: had the first stayed registered, the name would be ambiguous.
            manager.Load(ScriptAssemblyCompiler.CompileScriptAssembly(directory2, "ManagerReloaded", version: 2));

            Assert.Equal(2, ResolvedVersion());
        }
        finally
        {
            ElementFactory.UnregisterScriptAssembly(Assembly.LoadFile(Path.Combine(directory1, "ManagerReloaded.dll")));
            ElementFactory.UnregisterScriptAssembly(Assembly.LoadFile(Path.Combine(directory2, "ManagerReloaded.dll")));
            DeleteDirectory(directory1);
            DeleteDirectory(directory2);
        }
    }

    [Fact]
    public void ProvidedLoader_RegistersNothingAndKeepsNoReference()
    {
        string directory = CreateTempDirectory();
        var host = new ScriptAssemblyHost();
        try
        {
            string path = ScriptAssemblyCompiler.CompileScriptAssembly(directory, "ManagerProvidedLoader", version: 1);
            var manager = new AssemblyManager { AssemblyLoader = host.Load };

            manager.Load(path);

            Assert.True(host.IsLoaded);
            Assert.False(IsResolvable());
            Assert.True(host.Unload());
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void EditorShadowCopyReload_KeepsOnlyTheSecondAssemblyRegisteredAndCollectsTheFirst()
    {
        string directory1 = CreateTempDirectory();
        string directory2 = CreateTempDirectory();
        var manager = new AssemblyManager { AssemblyLoader = EditorScriptAssemblyService.LoadShadowCopy };
        try
        {
            string firstPath = ScriptAssemblyCompiler.CompileScriptAssembly(directory1, "ManagerEditorReload", version: 1);
            var firstAssembly = LoadAndTrack(manager, firstPath);
            Assert.Equal(1, ResolvedVersion());

            // Same full name in both versions: had the first stayed registered, the name would be ambiguous.
            manager.Load(ScriptAssemblyCompiler.CompileScriptAssembly(directory2, "ManagerEditorReload", version: 2));

            Assert.Equal(2, ResolvedVersion());
            Assert.True(HasLoadedAssembly());
            Assert.True(IsCollected(firstAssembly), "the first collectible context must be collected after the reload");
        }
        finally
        {
            // The result of Unload() is not asserted: in a Debug build the temporary that carries the assembly to
            // ElementFactory.UnregisterScriptAssembly keeps the context alive during its own verification, so the
            // service answers false although the context is collectible (the weak reference above is the proof).
            EditorScriptAssemblyService.Unload();
            DeleteDirectory(directory1);
            DeleteDirectory(directory2);
        }
    }

    // The assembly is read in its own frame: a temporary on the test frame would keep the context alive.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool HasLoadedAssembly() => EditorScriptAssemblyService.LoadedAssembly != null;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference LoadAndTrack(AssemblyManager manager, string path)
    {
        manager.Load(path);
        return new WeakReference(EditorScriptAssemblyService.LoadedAssembly);
    }

    private static bool IsCollected(WeakReference reference)
    {
        for (int i = 0; i < 10 && reference.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        return !reference.IsAlive;
    }
}

using System.Runtime.CompilerServices;
using CasaEngine.Engine.Plugins;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scripting;
using Xunit;

namespace CasaEngine.Tests.Scripting;

// ElementFactory caches are process-global: run serialized with the other
// environment-touching tests.
[Collection(ProjectEnvironmentCollection.Name)]
public class ElementFactoryReloadTests
{
    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "casa-factory-reload-" + Guid.NewGuid().ToString("N"));
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

    [Fact]
    public void RegisteredScriptAssembly_ResolvesByTypeName()
    {
        string directory = CreateTempDirectory();
        var host = new ScriptAssemblyHost();
        try
        {
            string assemblyPath = ScriptAssemblyCompiler.CompileScriptAssembly(directory, "FactoryScripts", version: 1);

            RegisterAndAssertResolvable(host, assemblyPath, expectedVersion: 1);
        }
        finally
        {
            UnregisterLoadedAssembly(host);
            host.Unload();
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void UnregisteredScriptAssembly_IsNoLongerResolvable()
    {
        string directory = CreateTempDirectory();
        var host = new ScriptAssemblyHost();
        try
        {
            string assemblyPath = ScriptAssemblyCompiler.CompileScriptAssembly(directory, "FactoryScriptsGone", version: 1);
            RegisterAndAssertResolvable(host, assemblyPath, expectedVersion: 1);

            UnregisterLoadedAssembly(host);

            // Unknown type name: the factory finds no type and Create fails.
            Assert.ThrowsAny<ArgumentException>(() => ElementFactory.Create<GameplayProxy>("TestScriptProxy"));
        }
        finally
        {
            host.Unload();
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void UnregisteredScriptAssembly_DoesNotPinTheLoadContext()
    {
        string directory = CreateTempDirectory();
        var host = new ScriptAssemblyHost();
        try
        {
            string assemblyPath = ScriptAssemblyCompiler.CompileScriptAssembly(directory, "FactoryScriptsUnload", version: 1);
            RegisterAndAssertResolvable(host, assemblyPath, expectedVersion: 1);

            UnregisterLoadedAssembly(host);

            // The factory dropped its cached types: the collectible context can die.
            Assert.True(host.Unload());
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>A game component with a [SerializedProperty], compiled into the script assembly.</summary>
    internal const string AnnotatedComponentSource = """
        using CasaEngine.Core.Serialization;
        using CasaEngine.Framework.Scene.Entities.Components;

        namespace TestScripts;

        public class TestScriptAnnotatedComponent : EntityComponent
        {
            [SerializedProperty("speed")]
            public float Speed { get; set; }

            public TestScriptAnnotatedComponent() { }

            private TestScriptAnnotatedComponent(TestScriptAnnotatedComponent other) : base(other)
            {
                SerializedProperties.Copy(other, this);
            }

            public override EntityComponent Clone() => new TestScriptAnnotatedComponent(this);
        }
        """;

    [Fact]
    public void UnregisteredScriptAssembly_WithAnnotatedComponent_DoesNotPinTheLoadContext()
    {
        string directory = CreateTempDirectory();
        var host = new ScriptAssemblyHost();
        try
        {
            string assemblyPath = ScriptAssemblyCompiler.CompileScriptAssembly(directory, "FactoryScriptsAnnotated", version: 1, AnnotatedComponentSource);
            LoadAnnotatedComponent(host, assemblyPath);

            UnregisterLoadedAssembly(host);

            // The serialized-property cache holds the component type weakly: the collectible context can die.
            Assert.True(host.Unload());
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void ReloadedScriptAssembly_ResolvesToTheNewVersion()
    {
        string directory1 = CreateTempDirectory();
        string directory2 = CreateTempDirectory();
        var host = new ScriptAssemblyHost();
        try
        {
            string v1Path = ScriptAssemblyCompiler.CompileScriptAssembly(directory1, "FactoryScriptsV", version: 1);
            RegisterAndAssertResolvable(host, v1Path, expectedVersion: 1);

            UnregisterLoadedAssembly(host);
            Assert.True(host.Unload());

            string v2Path = ScriptAssemblyCompiler.CompileScriptAssembly(directory2, "FactoryScriptsV", version: 2);
            RegisterAndAssertResolvable(host, v2Path, expectedVersion: 2);
        }
        finally
        {
            UnregisterLoadedAssembly(host);
            host.Unload();
            DeleteDirectory(directory1);
            DeleteDirectory(directory2);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RegisterAndAssertResolvable(ScriptAssemblyHost host, string assemblyPath, int expectedVersion)
    {
        var assembly = host.Load(assemblyPath);
        ElementFactory.RegisterScriptAssembly(assembly);

        var proxy = ElementFactory.Create<GameplayProxy>("TestScriptProxy");
        Assert.NotNull(proxy);

        var versionProperty = proxy!.GetType().GetProperty("Version");
        Assert.NotNull(versionProperty);
        Assert.Equal(expectedVersion, (int)versionProperty!.GetValue(null)!);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LoadAnnotatedComponent(ScriptAssemblyHost host, string assemblyPath)
    {
        var assembly = host.Load(assemblyPath);
        ElementFactory.RegisterScriptAssembly(assembly);

        var component = ElementFactory.Load<EntityComponent>(Newtonsoft.Json.Linq.JObject.Parse(
            """{ "id": "2d5b1f5e-3f0a-4c3e-9b54-6a1f2d3e4c5b", "name": "Annotated", "type": "TestScriptAnnotatedComponent", "speed": 12.5 }"""));

        Assert.Equal(12.5f, (float)component.GetType().GetProperty("Speed")!.GetValue(component)!);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void UnregisterLoadedAssembly(ScriptAssemblyHost host)
    {
        if (host.LoadedAssembly != null)
        {
            ElementFactory.UnregisterScriptAssembly(host.LoadedAssembly);
        }
    }
}

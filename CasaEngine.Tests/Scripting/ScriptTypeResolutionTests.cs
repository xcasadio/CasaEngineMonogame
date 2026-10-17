using System.Runtime.CompilerServices;
using CasaEngine.Engine.Plugins;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scripting;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Scripting;

/// <summary>A public top-level proxy with a public nested proxy, to check that nested types are refused.</summary>
public class ScriptResolutionOuterStub
{
    public class NestedProxy : GameplayProxy
    {
        public override IGameplayProxy Clone() => new NestedProxy();
        public override void InitializeWithWorld(World world) { }
        public override void Update(float elapsedTime) { }
        public override void Draw() { }
        public override void OnHit(Collision collision) { }
        public override void OnHitEnded(Collision collision) { }
        public override void OnBeginPlay(World world) { }
        public override void OnEndPlay(World world) { }
    }
}

/// <summary>A proxy that is public and top level but abstract: not instantiable.</summary>
public abstract class ScriptResolutionAbstractStub : GameplayProxy
{
}

/// <summary>
/// Script objects are resolved by exact full name, in the registered script assemblies only (ADR-0075, D1 and D3).
/// The test assembly is registered by <see cref="ScriptTestAssemblyRegistration"/>; the dynamic assemblies of
/// <see cref="ScriptAssemblyCompiler"/> are registered and unregistered by each test.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class ScriptTypeResolutionTests
{
    private const string TestProxyFullName = "TestScripts.TestScriptProxy";

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "casa-script-resolution-" + Guid.NewGuid().ToString("N"));
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
    private static void Register(ScriptAssemblyHost host, string assemblyPath) =>
        ElementFactory.RegisterScriptAssembly(host.Load(assemblyPath));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Unregister(ScriptAssemblyHost host)
    {
        if (host.LoadedAssembly != null)
        {
            ElementFactory.UnregisterScriptAssembly(host.LoadedAssembly);
        }
    }

    // No instance of a collectible type may stay on the test frame, or the context could not be collected.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AssertCreatable(string fullName) =>
        Assert.NotNull(ElementFactory.CreateScriptObject<GameplayProxy>(fullName, "script_class_name"));

    [Fact]
    public void FullName_ResolvesInARegisteredAssembly()
    {
        string directory = CreateTempDirectory();
        var host = new ScriptAssemblyHost();
        try
        {
            Register(host, ScriptAssemblyCompiler.CompileScriptAssembly(directory, "ResolveFullName", version: 1));

            var proxy = ElementFactory.CreateScriptObject<GameplayProxy>(TestProxyFullName, "script_class_name");

            Assert.NotNull(proxy);
            Assert.Equal(TestProxyFullName, proxy.GetType().FullName);
        }
        finally
        {
            Unregister(host);
            host.Unload();
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void FullName_ResolvesInTheTestAssembly()
    {
        var proxy = ElementFactory.CreateScriptObject<GameplayProxy>(typeof(StatefulEntityScriptStub).FullName, "script_class_name");

        Assert.IsType<StatefulEntityScriptStub>(proxy);
    }

    [Fact]
    public void ShortName_IsRefusedWithTheFullNameAsHint()
    {
        string directory = CreateTempDirectory();
        var host = new ScriptAssemblyHost();
        try
        {
            Register(host, ScriptAssemblyCompiler.CompileScriptAssembly(directory, "ResolveShortName", version: 1));

            var exception = Assert.Throws<ScriptTypeResolutionException>(
                () => ElementFactory.CreateScriptObject<GameplayProxy>("TestScriptProxy", "script_class_name"));

            Assert.Equal("script_class_name", exception.Key);
            Assert.Equal("TestScriptProxy", exception.Value);
            Assert.Contains("'TestScriptProxy'", exception.Message);
            Assert.Contains("script_class_name", exception.Message);
            Assert.Contains(TestProxyFullName, exception.Hint);
            Assert.Contains("Did you mean", exception.Message);
            Assert.Contains("ResolveShortName", exception.Message);
        }
        finally
        {
            Unregister(host);
            host.Unload();
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void FullNameOfAnEngineType_IsNotFound()
    {
        var exception = Assert.Throws<ScriptTypeResolutionException>(
            () => ElementFactory.CreateScriptObject<PlayerController>(typeof(PlayerController).FullName, "player_controller_class"));

        Assert.Equal(typeof(PlayerController).FullName, exception.Value);
        Assert.Contains(typeof(PlayerController).FullName!, exception.Message);
    }

    [Fact]
    public void TypeOfAnUnregisteredAssembly_IsNotFound()
    {
        string directory = CreateTempDirectory();
        var host = new ScriptAssemblyHost();
        try
        {
            // Loaded but never registered: not a gameplay assembly.
            host.Load(ScriptAssemblyCompiler.CompileScriptAssembly(directory, "NeverRegistered", version: 1));

            Assert.Throws<ScriptTypeResolutionException>(
                () => ElementFactory.CreateScriptObject<GameplayProxy>(TestProxyFullName, "script_class_name"));
        }
        finally
        {
            host.Unload();
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void TwoAssembliesWithTheSameFullName_AreAmbiguousAndBothAreCited()
    {
        string directory1 = CreateTempDirectory();
        string directory2 = CreateTempDirectory();
        var host1 = new ScriptAssemblyHost();
        var host2 = new ScriptAssemblyHost();
        try
        {
            Register(host1, ScriptAssemblyCompiler.CompileScriptAssembly(directory1, "AmbiguousFirst", version: 1));
            Register(host2, ScriptAssemblyCompiler.CompileScriptAssembly(directory2, "AmbiguousSecond", version: 2));

            var exception = Assert.Throws<ScriptTypeResolutionException>(
                () => ElementFactory.CreateScriptObject<GameplayProxy>(TestProxyFullName, "script_class_name"));

            Assert.Contains("ambiguous", exception.Message);
            Assert.Contains("AmbiguousFirst", exception.Message);
            Assert.Contains("AmbiguousSecond", exception.Message);
        }
        finally
        {
            Unregister(host1);
            Unregister(host2);
            host1.Unload();
            host2.Unload();
            DeleteDirectory(directory1);
            DeleteDirectory(directory2);
        }
    }

    [Fact]
    public void WrongKind_IsRefusedAndNamesTheExpectedKind()
    {
        var exception = Assert.Throws<ScriptTypeResolutionException>(
            () => ElementFactory.CreateScriptObject<PlayerController>(typeof(StatefulEntityScriptStub).FullName, "player_controller_class"));

        Assert.Contains(typeof(PlayerController).FullName!, exception.Message);
        Assert.Contains("not a", exception.Message);
    }

    [Fact]
    public void Casing_IsExactAndTheCorrectSpellingIsSuggested()
    {
        string wrongCase = typeof(StatefulEntityScriptStub).FullName!.ToLowerInvariant();

        var exception = Assert.Throws<ScriptTypeResolutionException>(
            () => ElementFactory.CreateScriptObject<GameplayProxy>(wrongCase, "script_class_name"));

        Assert.Contains(typeof(StatefulEntityScriptStub).FullName!, exception.Hint);
    }

    [Fact]
    public void NestedType_IsRefused()
    {
        string clr = typeof(ScriptResolutionOuterStub.NestedProxy).FullName!;
        string dotted = clr.Replace('+', '.');

        Assert.Throws<ScriptTypeResolutionException>(
            () => ElementFactory.CreateScriptObject<GameplayProxy>(clr, "script_class_name"));
        Assert.Throws<ScriptTypeResolutionException>(
            () => ElementFactory.CreateScriptObject<GameplayProxy>(dotted, "script_class_name"));
    }

    [Fact]
    public void AbstractType_IsRefusedAndTheMessageSaysWhy()
    {
        var exception = Assert.Throws<ScriptTypeResolutionException>(
            () => ElementFactory.CreateScriptObject<GameplayProxy>(typeof(ScriptResolutionAbstractStub).FullName, "script_class_name"));

        Assert.Contains("concrete", exception.Message);
    }

    [Fact]
    public void AfterUnregistration_TheTypeIsNotFoundAndTheContextIsCollected()
    {
        string directory = CreateTempDirectory();
        var host = new ScriptAssemblyHost();
        try
        {
            Register(host, ScriptAssemblyCompiler.CompileScriptAssembly(directory, "ResolveThenGone", version: 1));
            AssertCreatable(TestProxyFullName);

            Unregister(host);

            Assert.Throws<ScriptTypeResolutionException>(
                () => ElementFactory.CreateScriptObject<GameplayProxy>(TestProxyFullName, "script_class_name"));
            Assert.True(host.Unload());
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void WithoutAnyScriptAssembly_TheMessageSaysSo()
    {
        ScriptTestAssemblyRegistration.WithTestAssemblyUnregistered(() =>
        {
            var exception = Assert.Throws<ScriptTypeResolutionException>(
                () => ElementFactory.CreateScriptObject<GameplayProxy>(TestProxyFullName, "script_class_name"));

            Assert.Contains("no gameplay assembly is registered", exception.Message);
        });
    }

    [Fact]
    public void TryResolveScriptType_ReturnsTheMessageWithoutThrowing()
    {
        bool found = ElementFactory.TryResolveScriptType(typeof(GameplayProxy), "script_class_name",
            typeof(StatefulEntityScriptStub).FullName, out var type, out var message);
        Assert.True(found);
        Assert.Equal(typeof(StatefulEntityScriptStub), type);
        Assert.Equal(string.Empty, message);

        bool missing = ElementFactory.TryResolveScriptType(typeof(GameplayProxy), "script_class_name",
            "Nowhere.Missing", out var none, out var missingMessage);
        Assert.False(missing);
        Assert.Null(none);
        Assert.Contains("Nowhere.Missing", missingMessage);
    }

    [Fact]
    public void EmptyName_IsReportedAsEmpty()
    {
        var exception = Assert.Throws<ScriptTypeResolutionException>(
            () => ElementFactory.CreateScriptObject<GameplayProxy>(string.Empty, "script_class_name"));

        Assert.Contains("empty", exception.Message);
    }

    [Fact]
    public void FileName_IsCompletedOnceAndTheFirstOneIsKept()
    {
        var exception = Assert.Throws<ScriptTypeResolutionException>(
            () => ElementFactory.CreateScriptObject<GameplayProxy>("Nowhere.Missing", "script_class_name"));
        Assert.Null(exception.FileName);
        Assert.Contains("unknown file", exception.Message);

        exception.CompleteFileName("Worlds/Level.world");
        exception.CompleteFileName("Other.world");

        Assert.Equal("Worlds/Level.world", exception.FileName);
        Assert.Contains("Worlds/Level.world", exception.Message);
    }
}

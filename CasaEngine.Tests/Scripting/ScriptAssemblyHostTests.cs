using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using CasaEngine.Engine.Plugins;
using CasaEngine.Framework.Scripting;
using Xunit;

namespace CasaEngine.Tests.Scripting;

// Unload verification needs a quiet process: a concurrent xunit thread whose stack
// still holds a stale reference can transiently pin the collectible context. The
// ProjectEnvironment collection runs alone, without parallel tests.
[Collection(ProjectEnvironmentCollection.Name)]
public class ScriptAssemblyHostTests
{
    // In Debug builds every local of a live frame keeps its target alive, so any code
    // that touches the script assembly must run in a NoInlining helper: when it returns,
    // nothing pins the collectible context and Unload() can verify the collection.

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "casa-script-host-" + Guid.NewGuid().ToString("N"));
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
    public void Load_SharesEngineTypeIdentityWithDefaultContext()
    {
        string directory = CreateTempDirectory();
        try
        {
            string assemblyPath = ScriptAssemblyCompiler.CompileScriptAssembly(directory, "ScriptsIdentity", version: 1);
            var host = new ScriptAssemblyHost();

            AssertScriptTypeIdentity(host, assemblyPath);

            Assert.True(host.Unload());
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AssertScriptTypeIdentity(ScriptAssemblyHost host, string assemblyPath)
    {
        var assembly = host.Load(assemblyPath);

        Assert.True(host.IsLoaded);
        var scriptType = assembly.GetType("TestScripts.TestScriptProxy");
        Assert.NotNull(scriptType);

        // The script type must derive from THE engine GameplayProxy (default context).
        Assert.True(typeof(GameplayProxy).IsAssignableFrom(scriptType));
        Assert.NotSame(AssemblyLoadContext.GetLoadContext(typeof(GameplayProxy).Assembly),
            AssemblyLoadContext.GetLoadContext(assembly));

        var instance = Activator.CreateInstance(scriptType!);
        Assert.IsAssignableFrom<GameplayProxy>(instance);
    }

    [Fact]
    public void Unload_ReleasesTheAssemblyFile()
    {
        string directory = CreateTempDirectory();
        try
        {
            string assemblyPath = ScriptAssemblyCompiler.CompileScriptAssembly(directory, "ScriptsUnload", version: 1);
            var host = new ScriptAssemblyHost();

            LoadAndDiscard(host, assemblyPath);

            Assert.True(host.Unload());
            Assert.False(host.IsLoaded);

            // The collectible context is gone: the file is deletable again.
            File.Delete(assemblyPath);
            Assert.False(File.Exists(assemblyPath));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void Reload_PicksUpTheNewAssemblyVersion()
    {
        string directory1 = CreateTempDirectory();
        string directory2 = CreateTempDirectory();
        try
        {
            var host = new ScriptAssemblyHost();

            string v1Path = ScriptAssemblyCompiler.CompileScriptAssembly(directory1, "ScriptsReload", version: 1);
            Assert.Equal(1, LoadAndReadVersion(host, v1Path));
            Assert.True(host.Unload());

            string v2Path = ScriptAssemblyCompiler.CompileScriptAssembly(directory2, "ScriptsReload", version: 2);
            Assert.Equal(2, LoadAndReadVersion(host, v2Path));
            Assert.True(host.Unload());
        }
        finally
        {
            DeleteDirectory(directory1);
            DeleteDirectory(directory2);
        }
    }

    [Fact]
    public void Load_Twice_WithoutUnload_Throws()
    {
        string directory = CreateTempDirectory();
        try
        {
            string assemblyPath = ScriptAssemblyCompiler.CompileScriptAssembly(directory, "ScriptsDouble", version: 1);
            var host = new ScriptAssemblyHost();

            LoadAndDiscard(host, assemblyPath);
            Assert.Throws<InvalidOperationException>(() => LoadAndDiscard(host, assemblyPath));

            Assert.True(host.Unload());
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LoadAndDiscard(ScriptAssemblyHost host, string assemblyPath)
    {
        host.Load(assemblyPath);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int LoadAndReadVersion(ScriptAssemblyHost host, string assemblyPath)
    {
        var assembly = host.Load(assemblyPath);
        var type = assembly.GetType("TestScripts.TestScriptProxy");
        Assert.NotNull(type);
        var property = type!.GetProperty("Version");
        Assert.NotNull(property);
        return (int)property!.GetValue(null)!;
    }
}

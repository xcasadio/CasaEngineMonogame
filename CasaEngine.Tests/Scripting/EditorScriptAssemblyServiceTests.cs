using CasaEngine.EditorServices.Scripting;
using Xunit;

namespace CasaEngine.Tests.Scripting;

// Touches the process-global ElementFactory registration: serialized collection.
[Collection(ProjectEnvironmentCollection.Name)]
public class EditorScriptAssemblyServiceTests
{
    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "casa-script-service-" + Guid.NewGuid().ToString("N"));
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
    public void LoadShadowCopy_KeepsTheOriginalFileWritable()
    {
        string directory = CreateTempDirectory();
        try
        {
            string dllPath = ScriptAssemblyCompiler.CompileScriptAssembly(directory, "ShadowScripts", version: 1);

            EditorScriptAssemblyService.LoadShadowCopy(dllPath);
            Assert.True(EditorScriptAssemblyService.IsLoaded);
            Assert.Equal(Path.GetFullPath(dllPath), EditorScriptAssemblyService.LoadedSourcePath);

            // The original dll is not locked: a rebuild can overwrite it while loaded.
            File.Delete(dllPath);
            Assert.False(File.Exists(dllPath));
        }
        finally
        {
            EditorScriptAssemblyService.Unload();
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void LoadShadowCopy_ReplacesThePreviousAssembly()
    {
        string directory1 = CreateTempDirectory();
        string directory2 = CreateTempDirectory();
        try
        {
            string v1Path = ScriptAssemblyCompiler.CompileScriptAssembly(directory1, "ShadowScriptsSwap", version: 1);
            string v2Path = ScriptAssemblyCompiler.CompileScriptAssembly(directory2, "ShadowScriptsSwap", version: 2);

            EditorScriptAssemblyService.LoadShadowCopy(v1Path);
            EditorScriptAssemblyService.LoadShadowCopy(v2Path);

            Assert.True(EditorScriptAssemblyService.IsLoaded);
            Assert.Equal(Path.GetFullPath(v2Path), EditorScriptAssemblyService.LoadedSourcePath);
        }
        finally
        {
            EditorScriptAssemblyService.Unload();
            DeleteDirectory(directory1);
            DeleteDirectory(directory2);
        }
    }

    [Fact]
    public void Unload_WithoutLoad_IsIdempotent()
    {
        Assert.True(EditorScriptAssemblyService.Unload());
        Assert.False(EditorScriptAssemblyService.IsLoaded);
    }
}

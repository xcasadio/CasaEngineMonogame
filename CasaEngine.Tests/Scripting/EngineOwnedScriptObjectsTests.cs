using System.Text.RegularExpressions;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scripting;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Scripting;

/// <summary>
/// Engine-owned script objects are created by code, never by a name (ADR-0075), and the data of the RPGDemo sample
/// names its scripts by full name.
/// </summary>
public class EngineOwnedScriptObjectsTests
{
    [Fact]
    public void SetGameplayProxy_KeepsTheInstanceAndRecordsItsFullName()
    {
        var entity = new Entity();
        var proxy = new ScriptArcBallCamera();

        entity.SetGameplayProxy(proxy);

        Assert.Same(proxy, entity.GameplayProxy);
        Assert.Equal(typeof(ScriptArcBallCamera).FullName, entity.GameplayProxyClassName);
    }

    [Fact]
    public void SetGameplayProxy_SurvivesInitializeWithoutAnyRegisteredScriptAssembly()
    {
        var entity = new Entity();
        var proxy = new ScriptArcBallCamera();
        entity.SetGameplayProxy(proxy);

        entity.Initialize();

        Assert.Same(proxy, entity.GameplayProxy);
    }

    [Fact]
    public void SetGameplayProxy_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new Entity().SetGameplayProxy(null!));
    }

    [Fact]
    public void RpgGameMode_UsesTheEngineControllerCreatedByCode()
    {
        var document = JObject.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "Projects", "RPGDemo", "RpgGameMode.gameMode")));

        var settings = new PlayerStartupSettings();
        settings.Load(document);

        Assert.Equal(string.Empty, settings.PlayerControllerClass);
    }

    [Fact]
    public void RpgDemoData_NamesEveryScriptByTheFullNameOfAnExistingClass()
    {
        var root = RepoRoot();
        var dataDirectory = Path.Combine(root, "Projects", "RPGDemo");
        var scriptsDirectory = Path.Combine(root, "Projects", "CasaEngine.RPGDemo", "Scripts");
        var checkedNames = 0;

        foreach (var path in Directory.EnumerateFiles(dataDirectory, "*.*", SearchOption.AllDirectories)
                     .Where(p => p.EndsWith(".world", StringComparison.Ordinal) || p.EndsWith(".entity", StringComparison.Ordinal)))
        {
            foreach (var name in ScriptNames(JToken.Parse(File.ReadAllText(path))))
            {
                var separator = name.LastIndexOf('.');
                Assert.True(separator > 0, $"{path}: script_class_name '{name}' is not a full name");
                var source = Path.Combine(scriptsDirectory, name[(separator + 1)..] + ".cs");
                Assert.True(File.Exists(source), $"{path}: no source file for '{name}'");
                var declared = Regex.Match(File.ReadAllText(source), @"^namespace\s+([\w.]+)\s*;", RegexOptions.Multiline);
                Assert.True(declared.Success && declared.Groups[1].Value == name[..separator],
                    $"{path}: '{name}' does not match the namespace of {source}");
                checkedNames++;
            }
        }

        Assert.Equal(8, checkedNames);
    }

    private static IEnumerable<string> ScriptNames(JToken token)
    {
        foreach (var property in token.SelectTokens("$..script_class_name"))
        {
            if (property.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string?)property))
            {
                yield return (string)property!;
            }
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Directory.Packages.props")))
        {
            dir = dir.Parent;
        }

        return dir!.FullName;
    }
}

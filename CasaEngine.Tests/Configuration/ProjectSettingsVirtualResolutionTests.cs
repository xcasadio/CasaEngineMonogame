using CasaEngine.Engine;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Configuration.Project;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Configuration;

/// <summary>
/// <see cref="ProjectSettings.VirtualResolution"/> is optional (ADR-0048): a project file without it loads as null
/// (even right after a project that had one), the key is written only when the setting is set, so a project without
/// it keeps a byte-identical file, and an invalid declaration fails at load with the key named.
/// </summary>
// Mutates the global project settings and engine environment: serialized collection.
[Collection(ProjectEnvironmentCollection.Name)]
public class ProjectSettingsVirtualResolutionTests
{
    private static string NewDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "casa-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void WithCleanGlobals(string directory, Action body)
    {
        string previousProjectPath = EngineEnvironment.ProjectPath;
        var previous = GameSettings.ProjectSettings.VirtualResolution;
        try
        {
            body();
        }
        finally
        {
            GameSettings.ProjectSettings.VirtualResolution = previous;
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string WriteProject(string directory, string name, JObject virtualResolution)
    {
        string fileName = Path.Combine(directory, name);
        var root = new JObject
        {
            ["WindowTitle"] = "t",
            ["ProjectName"] = "p",
            ["FirstScreenName"] = "",
            ["FirstWorldLoaded"] = "",
            ["GameplayDllName"] = "",
        };

        if (virtualResolution != null)
        {
            root["VirtualResolution"] = virtualResolution;
        }

        File.WriteAllText(fileName, root.ToString());
        return fileName;
    }

    [Fact]
    public void Load_ReadsWidthHeightAndMode()
    {
        string directory = NewDirectory();
        WithCleanGlobals(directory, () =>
        {
            string fileName = WriteProject(directory, "P.json",
                new JObject { ["Width"] = 320, ["Height"] = 240, ["Mode"] = "IntegerFit" });

            ProjectSettingsHelper.Load(fileName);

            var setting = GameSettings.ProjectSettings.VirtualResolution;
            Assert.NotNull(setting);
            Assert.Equal(320, setting.Width);
            Assert.Equal(240, setting.Height);
            Assert.Equal(VirtualResolutionMode.IntegerFit, setting.Mode);
        });
    }

    [Fact]
    public void Load_WithoutTheMode_DefaultsToIntegerFit()
    {
        string directory = NewDirectory();
        WithCleanGlobals(directory, () =>
        {
            string fileName = WriteProject(directory, "P.json", new JObject { ["Width"] = 256, ["Height"] = 224 });

            ProjectSettingsHelper.Load(fileName);

            Assert.Equal(VirtualResolutionMode.IntegerFit, GameSettings.ProjectSettings.VirtualResolution.Mode);
        });
    }

    [Fact]
    public void Load_WithoutTheKey_IsNull_EvenAfterLoadingAProjectThatHadOne()
    {
        string directory = NewDirectory();
        WithCleanGlobals(directory, () =>
        {
            string withIt = WriteProject(directory, "With.json", new JObject { ["Width"] = 320, ["Height"] = 240 });
            string without = WriteProject(directory, "Without.json", null);

            ProjectSettingsHelper.Load(withIt);
            Assert.NotNull(GameSettings.ProjectSettings.VirtualResolution);

            // Absent must mean absent, not "keep the previous value".
            ProjectSettingsHelper.Load(without);
            Assert.Null(GameSettings.ProjectSettings.VirtualResolution);
        });
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsTheSetting()
    {
        string directory = NewDirectory();
        WithCleanGlobals(directory, () =>
        {
            string fileName = Path.Combine(directory, "Project.json");
            GameSettings.ProjectSettings.VirtualResolution = new VirtualResolutionSettings { Width = 320, Height = 240 };

            ProjectSettingsHelper.Save(fileName);

            var document = JObject.Parse(File.ReadAllText(fileName));
            Assert.Equal(320, (int?)document["VirtualResolution"]?["Width"]);
            Assert.Equal(240, (int?)document["VirtualResolution"]?["Height"]);
            Assert.Equal("IntegerFit", (string)document["VirtualResolution"]?["Mode"]);

            GameSettings.ProjectSettings.VirtualResolution = null;
            ProjectSettingsHelper.Load(fileName);
            Assert.Equal(320, GameSettings.ProjectSettings.VirtualResolution.Width);
            Assert.Equal(240, GameSettings.ProjectSettings.VirtualResolution.Height);
        });
    }

    [Fact]
    public void Save_WithoutTheSetting_DoesNotWriteTheKey()
    {
        string directory = NewDirectory();
        WithCleanGlobals(directory, () =>
        {
            string fileName = Path.Combine(directory, "Project.json");
            GameSettings.ProjectSettings.VirtualResolution = null;

            ProjectSettingsHelper.Save(fileName);

            Assert.Null(JObject.Parse(File.ReadAllText(fileName))["VirtualResolution"]);
        });
    }

    [Theory]
    [InlineData(null, 240, "IntegerFit", "Width")]
    [InlineData(320, null, "IntegerFit", "Height")]
    [InlineData(0, 240, "IntegerFit", "Width")]
    [InlineData(320, -1, "IntegerFit", "Height")]
    [InlineData(320, 240, "Stretch", "Mode")]
    public void Load_WithAnInvalidDeclaration_FailsNamingTheKey(int? width, int? height, string mode, string key)
    {
        string directory = NewDirectory();
        WithCleanGlobals(directory, () =>
        {
            var declaration = new JObject { ["Mode"] = mode };
            if (width.HasValue)
            {
                declaration["Width"] = width.Value;
            }

            if (height.HasValue)
            {
                declaration["Height"] = height.Value;
            }

            string fileName = WriteProject(directory, "P.json", declaration);

            var error = Assert.Throws<InvalidDataException>(() => ProjectSettingsHelper.Load(fileName));

            Assert.Contains("VirtualResolution", error.Message);
            Assert.Contains(key, error.Message);
        });
    }
}

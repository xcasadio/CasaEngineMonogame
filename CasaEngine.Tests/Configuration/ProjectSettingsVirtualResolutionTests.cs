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

    private static string WriteProject(string directory, string name, JToken virtualResolution)
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

    // ADR-0086: the two buffered modes are read like IntegerFit, and every mode survives a save and a load.
    [Theory]
    [InlineData("IntegerFit", VirtualResolutionMode.IntegerFit)]
    [InlineData("BufferedIntegerFit", VirtualResolutionMode.BufferedIntegerFit)]
    [InlineData("BufferedFit", VirtualResolutionMode.BufferedFit)]
    public void Load_ThenSave_RoundTripsEveryMode(string modeName, VirtualResolutionMode expected)
    {
        string directory = NewDirectory();
        WithCleanGlobals(directory, () =>
        {
            string fileName = WriteProject(directory, "P.json",
                new JObject { ["Width"] = 320, ["Height"] = 240, ["Mode"] = modeName });

            ProjectSettingsHelper.Load(fileName);
            Assert.Equal(expected, GameSettings.ProjectSettings.VirtualResolution.Mode);

            string saved = Path.Combine(directory, "Saved.json");
            ProjectSettingsHelper.Save(saved);
            Assert.Equal(modeName, (string)JObject.Parse(File.ReadAllText(saved))["VirtualResolution"]?["Mode"]);

            GameSettings.ProjectSettings.VirtualResolution = null;
            ProjectSettingsHelper.Load(saved);
            Assert.Equal(expected, GameSettings.ProjectSettings.VirtualResolution.Mode);
        });
    }

    [Theory]
    [InlineData(VirtualResolutionMode.IntegerFit, false)]
    [InlineData(VirtualResolutionMode.BufferedIntegerFit, true)]
    [InlineData(VirtualResolutionMode.BufferedFit, true)]
    public void IsBuffered_IsTrueOnlyForTheBufferedModes(VirtualResolutionMode mode, bool expected)
    {
        Assert.Equal(expected, mode.IsBuffered());
    }

    [Fact]
    public void Load_WithAnUnknownMode_ListsTheSupportedModes()
    {
        string directory = NewDirectory();
        WithCleanGlobals(directory, () =>
        {
            string fileName = WriteProject(directory, "P.json",
                new JObject { ["Width"] = 320, ["Height"] = 240, ["Mode"] = "Stretch" });

            var error = Assert.Throws<InvalidDataException>(() => ProjectSettingsHelper.Load(fileName));

            Assert.Contains("IntegerFit", error.Message);
            Assert.Contains("BufferedIntegerFit", error.Message);
            Assert.Contains("BufferedFit", error.Message);
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

    [Fact]
    public void Load_WithoutScaleUI_IsFalse()
    {
        string directory = NewDirectory();
        WithCleanGlobals(directory, () =>
        {
            string fileName = WriteProject(directory, "P.json", new JObject { ["Width"] = 320, ["Height"] = 180 });

            ProjectSettingsHelper.Load(fileName);

            Assert.False(GameSettings.ProjectSettings.VirtualResolution.ScaleUI);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Load_ReadsScaleUI(bool value)
    {
        string directory = NewDirectory();
        WithCleanGlobals(directory, () =>
        {
            string fileName = WriteProject(directory, "P.json",
                new JObject { ["Width"] = 320, ["Height"] = 180, ["ScaleUI"] = value });

            ProjectSettingsHelper.Load(fileName);

            Assert.Equal(value, GameSettings.ProjectSettings.VirtualResolution.ScaleUI);
        });
    }

    [Fact]
    public void Save_WithScaleUI_RoundTripsIt()
    {
        string directory = NewDirectory();
        WithCleanGlobals(directory, () =>
        {
            string fileName = Path.Combine(directory, "Project.json");
            GameSettings.ProjectSettings.VirtualResolution = new VirtualResolutionSettings { Width = 320, Height = 180, ScaleUI = true };

            ProjectSettingsHelper.Save(fileName);

            var document = JObject.Parse(File.ReadAllText(fileName));
            Assert.True((bool?)document["VirtualResolution"]?["ScaleUI"]);

            GameSettings.ProjectSettings.VirtualResolution = null;
            ProjectSettingsHelper.Load(fileName);
            Assert.True(GameSettings.ProjectSettings.VirtualResolution.ScaleUI);
        });
    }

    [Fact]
    public void Save_WithoutScaleUI_DoesNotWriteTheKey()
    {
        string directory = NewDirectory();
        WithCleanGlobals(directory, () =>
        {
            string fileName = Path.Combine(directory, "Project.json");
            GameSettings.ProjectSettings.VirtualResolution = new VirtualResolutionSettings { Width = 320, Height = 180 };

            ProjectSettingsHelper.Save(fileName);

            var declaration = (JObject)JObject.Parse(File.ReadAllText(fileName))["VirtualResolution"];
            Assert.False(declaration.ContainsKey("ScaleUI"));
        });
    }

    [Theory]
    [InlineData("\"true\"")]
    [InlineData("1")]
    [InlineData("null")]
    [InlineData("{}")]
    public void Load_WithAScaleUIThatIsNotABoolean_FailsNamingTheKey(string json)
    {
        string directory = NewDirectory();
        WithCleanGlobals(directory, () =>
        {
            var declaration = new JObject { ["Width"] = 320, ["Height"] = 180, ["ScaleUI"] = JToken.Parse(json) };
            string fileName = WriteProject(directory, "P.json", declaration);

            var error = Assert.Throws<InvalidDataException>(() => ProjectSettingsHelper.Load(fileName));

            Assert.Contains("VirtualResolution.ScaleUI", error.Message);
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

    // Only a JSON integer in 1..int.MaxValue is a dimension: a numeric string, a boolean, a float or an out-of-range
    // integer used to load silently (or throw without naming the key).
    [Theory]
    [InlineData("\"abc\"")]
    [InlineData("\"320\"")]
    [InlineData("true")]
    [InlineData("1.5")]
    [InlineData("320.0")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("3000000000")]
    [InlineData("99999999999999999999")]
    [InlineData("null")]
    public void Load_WithADimensionThatIsNotAPositiveInteger_FailsNamingTheKey(string json)
    {
        foreach (string key in new[] { "Width", "Height" })
        {
            string directory = NewDirectory();
            WithCleanGlobals(directory, () =>
            {
                var declaration = new JObject { ["Width"] = 320, ["Height"] = 240 };
                declaration[key] = JToken.Parse(json);
                string fileName = WriteProject(directory, "P.json", declaration);

                var error = Assert.Throws<InvalidDataException>(() => ProjectSettingsHelper.Load(fileName));

                Assert.Contains("VirtualResolution." + key, error.Message);
            });
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Load_WithADimensionAtABound_LoadsIt(int dimension)
    {
        string directory = NewDirectory();
        WithCleanGlobals(directory, () =>
        {
            string fileName = WriteProject(directory, "P.json",
                new JObject { ["Width"] = dimension, ["Height"] = dimension });

            ProjectSettingsHelper.Load(fileName);

            var setting = GameSettings.ProjectSettings.VirtualResolution;
            Assert.Equal(dimension, setting.Width);
            Assert.Equal(dimension, setting.Height);
        });
    }

    [Theory]
    [InlineData("\"320x240\"")]
    [InlineData("[320,240]")]
    [InlineData("5")]
    public void Load_WithAVirtualResolutionThatIsNotAnObject_FailsNamingTheSetting(string json)
    {
        string directory = NewDirectory();
        WithCleanGlobals(directory, () =>
        {
            string fileName = WriteProject(directory, "P.json", JToken.Parse(json));

            var error = Assert.Throws<InvalidDataException>(() => ProjectSettingsHelper.Load(fileName));

            Assert.Contains("VirtualResolution", error.Message);
        });
    }
}

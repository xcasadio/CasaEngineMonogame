using CasaEngine.Core.Logging;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Configuration.Project;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Audio;

/// <summary>
/// The Master limiter project setting (decision D32): <see cref="ProjectSettings.IsMasterLimiterEnabled"/> is additive
/// (absent = on, written only when set), applied at startup and on every editor project load.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class ProjectMasterLimiterSettingTests
{
    private sealed class CapturingLogger : ILogger
    {
        public List<string> Warnings { get; } = new();

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) { }
        public void WriteWarning(string msg) => Warnings.Add(msg);
        public void WriteError(string msg) { }
    }

    [Fact]
    public void Save_WithoutSetting_WritesNoKey_AndTheFileIsUnchanged()
    {
        WithTempProject((directory, fileName) =>
        {
            GameSettings.ProjectSettings.IsMasterLimiterEnabled = null;
            ProjectSettingsHelper.Save(fileName);
            string first = File.ReadAllText(fileName);

            Assert.Null(JObject.Parse(first)["IsMasterLimiterEnabled"]);

            ProjectSettingsHelper.Load(fileName);
            Assert.Null(GameSettings.ProjectSettings.IsMasterLimiterEnabled);
            ProjectSettingsHelper.Save(fileName);
            Assert.Equal(first, File.ReadAllText(fileName));
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Save_ThenLoad_RoundTripsTheSetting(bool value)
    {
        WithTempProject((directory, fileName) =>
        {
            GameSettings.ProjectSettings.IsMasterLimiterEnabled = value;
            ProjectSettingsHelper.Save(fileName);

            Assert.Equal(value, (bool)JObject.Parse(File.ReadAllText(fileName))["IsMasterLimiterEnabled"]!);

            GameSettings.ProjectSettings.IsMasterLimiterEnabled = null;
            ProjectSettingsHelper.Load(fileName);
            Assert.Equal(value, GameSettings.ProjectSettings.IsMasterLimiterEnabled);
        });
    }

    [Fact]
    public void Load_WithoutTheKey_IsNull_EvenAfterLoadingAProjectThatSetsIt()
    {
        WithTempProject((directory, fileName) =>
        {
            string withKey = Path.Combine(directory, "With.json");
            GameSettings.ProjectSettings.IsMasterLimiterEnabled = false;
            ProjectSettingsHelper.Save(withKey);
            GameSettings.ProjectSettings.IsMasterLimiterEnabled = null;
            ProjectSettingsHelper.Save(fileName);

            ProjectSettingsHelper.Load(withKey);
            Assert.False(GameSettings.ProjectSettings.IsMasterLimiterEnabled);

            ProjectSettingsHelper.Load(fileName);
            Assert.Null(GameSettings.ProjectSettings.IsMasterLimiterEnabled);
        });
    }

    [Fact]
    public void Apply_WithSettingFalse_SwitchesTheLimiterOff_AndMutesAsBefore()
    {
        using var service = new AudioService(new SoftwareAudioBackend(new OfflineAudioOutput(), 16));
        Assert.True(service.MasterLimiter.IsEnabled);

        ProjectAudioSettings.Apply(service, new ProjectSettings { IsMasterLimiterEnabled = false, IsAudioMuted = true });

        Assert.False(service.MasterLimiter.IsEnabled);
        Assert.True(service.Mixer.GetBus(AudioBusNames.Master).IsMuted);
    }

    [Fact]
    public void Apply_WithSettingAbsent_SwitchesTheLimiterBackOn()
    {
        using var service = new AudioService(new SoftwareAudioBackend(new OfflineAudioOutput(), 16));
        service.MasterLimiter.IsEnabled = false;

        ProjectAudioSettings.Apply(service, new ProjectSettings());

        Assert.True(service.MasterLimiter.IsEnabled);
    }

    [Fact]
    public void Apply_UnderABackendWithoutTheCapability_LogsNothingAboutTheLimiter_AndMutes()
    {
        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            using var service = new AudioService(new FakeAudioBackend());

            ProjectAudioSettings.Apply(service, new ProjectSettings { IsMasterLimiterEnabled = false, IsAudioMuted = true });

            Assert.True(service.Mixer.GetBus(AudioBusNames.Master).IsMuted);
        }
        finally
        {
            Logs.Close();
        }

        Assert.DoesNotContain(logger.Warnings, warning => warning.Contains("limiter"));
    }

    [Fact]
    public void Editor_ProjectLoaded_FollowsTheLimiterAndTheMuteOfEachProject()
    {
        WithTempProject((directory, _) =>
        {
            string projectA = Path.Combine(directory, "A.json");
            string projectB = Path.Combine(directory, "B.json");

            bool previousMute = GameSettings.ProjectSettings.IsAudioMuted;
            using var service = new AudioService(new SoftwareAudioBackend(new OfflineAudioOutput(), 16));
            var sync = new EditorProjectAudioMuteSync(service);
            try
            {
                GameSettings.ProjectSettings.IsMasterLimiterEnabled = false;
                GameSettings.ProjectSettings.IsAudioMuted = true;
                ProjectSettingsHelper.Save(projectA);

                GameSettings.ProjectSettings.IsMasterLimiterEnabled = null;
                GameSettings.ProjectSettings.IsAudioMuted = false;
                ProjectSettingsHelper.Save(projectB);

                EditorProjectAuthoringService.LoadProject(projectA);
                Assert.False(service.MasterLimiter.IsEnabled);
                Assert.True(service.Mixer.GetBus(AudioBusNames.Master).IsMuted);

                EditorProjectAuthoringService.LoadProject(projectB);
                Assert.True(service.MasterLimiter.IsEnabled);
                Assert.False(service.Mixer.GetBus(AudioBusNames.Master).IsMuted);

                sync.Dispose();
                EditorProjectAuthoringService.LoadProject(projectA);
                Assert.True(service.MasterLimiter.IsEnabled);
            }
            finally
            {
                sync.Dispose();
                EditorProjectAuthoringService.ClearProject();
                GameSettings.ProjectSettings.IsAudioMuted = previousMute;
            }
        });
    }

    private static void WithTempProject(Action<string, string> body)
    {
        string directory = Path.Combine(Path.GetTempPath(), "casa-limiter-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string fileName = Path.Combine(directory, "Project.json");

        string? previousProjectPath = EngineEnvironment.ProjectPath;
        bool? previousLimiter = GameSettings.ProjectSettings.IsMasterLimiterEnabled;
        try
        {
            body(directory, fileName);
        }
        finally
        {
            GameSettings.ProjectSettings.IsMasterLimiterEnabled = previousLimiter;
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(directory, recursive: true);
        }
    }
}

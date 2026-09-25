using CasaEngine.Core.Logging;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Configuration.Project;

namespace CasaEngine.Framework.Audio;

/// <summary>
/// Applies <see cref="ProjectSettings.IsAudioMuted"/> (ADR-0040) to an <see cref="AudioMixer"/>'s
/// <see cref="AudioBusNames.Master"/> bus. Pure and testable: no <c>Game</c>, no editor type, no
/// MonoGame dependency, so both the runtime startup path and the editor's project-load path can
/// share it.
/// </summary>
public static class ProjectAudioSettings
{
    /// <summary>
    /// Mutes or unmutes <paramref name="mixer"/>'s Master bus according to
    /// <paramref name="projectSettings"/>. Called once at startup (runtime) or on every project
    /// load (editor), never per frame.
    /// </summary>
    public static void Apply(AudioMixer mixer, ProjectSettings projectSettings)
    {
        ArgumentNullException.ThrowIfNull(mixer);
        ArgumentNullException.ThrowIfNull(projectSettings);

        mixer.GetBus(AudioBusNames.Master).IsMuted = projectSettings.IsAudioMuted;
        LogIfMuted(projectSettings.IsAudioMuted);
    }

    /// <summary>
    /// Sets the Master bus mute and mirrors <paramref name="isMuted"/> into
    /// <paramref name="projectSettings"/> and, when it is a different instance, into
    /// <paramref name="globalProjectSettings"/> (same idea as
    /// <c>CasaEngineGame.ApplyDisplaySettings</c>), so the change survives an editor save. Never
    /// writes the project file itself.
    /// </summary>
    public static void SetMuted(AudioMixer mixer, bool isMuted, ProjectSettings projectSettings, ProjectSettings globalProjectSettings)
    {
        ArgumentNullException.ThrowIfNull(mixer);
        ArgumentNullException.ThrowIfNull(projectSettings);
        ArgumentNullException.ThrowIfNull(globalProjectSettings);

        mixer.GetBus(AudioBusNames.Master).IsMuted = isMuted;

        projectSettings.IsAudioMuted = isMuted;
        if (!ReferenceEquals(projectSettings, globalProjectSettings))
        {
            globalProjectSettings.IsAudioMuted = isMuted;
        }

        LogIfMuted(isMuted);
    }

    private static void LogIfMuted(bool isMuted)
    {
        if (isMuted)
        {
            Logs.WriteInfo("The project settings mute the Master audio bus.");
        }
    }
}

using CasaEngine.Framework.Application;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Configuration.Project;

namespace CasaEngine.EditorServices;

/// <summary>
/// Keeps an editor audio mixer's Master bus mute in sync with the currently loaded project
/// (ADR-0040): muting `Master` in the editor also silences asset previews, because the `Editor`
/// bus hangs from `Master`.
/// </summary>
/// <remarks>
/// Subscribes to <see cref="EditorProjectAuthoringService.ProjectLoaded"/> for the life of the
/// instance and applies the loaded settings through <see cref="ProjectAudioSettings.Apply"/> on
/// every event; <see cref="Dispose"/> unsubscribes and is idempotent.
/// <see cref="EditorProjectAuthoringService.LoadProject"/> always raises the event with
/// <c>GameSettings.ProjectSettings</c> as the sender, and the editor always calls
/// <c>LoadProject(fileName)</c> without an explicit <see cref="EngineRuntimeContext"/> (which
/// would otherwise make <c>EngineRuntimeContext.FromGlobals</c>, and therefore the loaded
/// settings, point at a different instance) — so in the editor the sender IS the instance that
/// was just loaded. This reads the sender as a <see cref="ProjectSettings"/> and falls back to
/// <c>GameSettings.ProjectSettings</c> when it is not, so the sync stays correct even if a caller
/// ever raises the event differently.
/// </remarks>
public sealed class EditorProjectAudioMuteSync : IDisposable
{
    private readonly AudioMixer _mixer;
    private bool _isDisposed;

    public EditorProjectAudioMuteSync(AudioMixer mixer)
    {
        ArgumentNullException.ThrowIfNull(mixer);

        _mixer = mixer;
        EditorProjectAuthoringService.ProjectLoaded += OnProjectLoaded;
    }

    private void OnProjectLoaded(object? sender, EventArgs e)
    {
        var projectSettings = sender as ProjectSettings ?? GameSettings.ProjectSettings;
        ProjectAudioSettings.Apply(_mixer, projectSettings);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        EditorProjectAuthoringService.ProjectLoaded -= OnProjectLoaded;
        _isDisposed = true;
    }
}

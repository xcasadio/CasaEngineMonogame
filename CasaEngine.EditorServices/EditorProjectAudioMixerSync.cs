using CasaEngine.Framework.Application;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Configuration.Project;

namespace CasaEngine.EditorServices;

/// <summary>
/// Keeps the editor's live mixer in line with the loaded project's <c>.audioMixer</c> asset (plan decision P48): applies
/// <see cref="ProjectSettings.AudioMixerAsset"/> on every <see cref="EditorProjectAuthoringService.ProjectLoaded"/> and
/// releases the asset on <see cref="EditorProjectAuthoringService.ProjectClosed"/>.
/// </summary>
/// <remarks>
/// Same wiring as <see cref="EditorProjectAudioMuteSync"/>: the sender of <c>ProjectLoaded</c> is the loaded
/// <see cref="ProjectSettings"/>, with <c>GameSettings.ProjectSettings</c> as the fallback. After each apply or release,
/// <see cref="ProjectMixerApplied"/> carries <see cref="ProjectAudioMixer.AppliedAssetId"/> (<see cref="Guid.Empty"/>
/// when none) so the editor can attach its open mixing documents. <see cref="Dispose"/> unsubscribes and is idempotent.
/// </remarks>
public sealed class EditorProjectAudioMixerSync : IDisposable
{
    private readonly ProjectAudioMixer _mixer;
    private bool _isDisposed;

    public EditorProjectAudioMixerSync(ProjectAudioMixer mixer)
    {
        ArgumentNullException.ThrowIfNull(mixer);

        _mixer = mixer;
        EditorProjectAuthoringService.ProjectLoaded += OnProjectLoaded;
        EditorProjectAuthoringService.ProjectClosed += OnProjectClosed;
    }

    /// <summary>Raised after the project's mixer asset was applied or released, with the applied asset id or <see cref="Guid.Empty"/>.</summary>
    public event EventHandler<Guid> ProjectMixerApplied;

    private void OnProjectLoaded(object sender, EventArgs e)
    {
        var projectSettings = sender as ProjectSettings ?? GameSettings.ProjectSettings;
        _mixer.Apply(projectSettings);
        ProjectMixerApplied?.Invoke(this, _mixer.AppliedAssetId);
    }

    private void OnProjectClosed(object sender, EventArgs e)
    {
        // No settings means no asset: releases the applier and clears AppliedAssetId.
        _mixer.Apply(null);
        ProjectMixerApplied?.Invoke(this, _mixer.AppliedAssetId);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        EditorProjectAuthoringService.ProjectLoaded -= OnProjectLoaded;
        EditorProjectAuthoringService.ProjectClosed -= OnProjectClosed;
        _isDisposed = true;
    }
}

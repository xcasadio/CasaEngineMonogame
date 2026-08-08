#nullable enable

using System;
using CasaEngine.Editor.Controls.Timeline.Playback;

namespace CasaEngine.Editor.Controls;

internal sealed class Animation2dTimelinePlaybackController : ITimelinePlaybackController
{
    private readonly Func<float> _getCurrentTime;
    private readonly Action<float> _seek;

    public Animation2dTimelinePlaybackController(Func<float> getCurrentTime, Action<float> seek)
    {
        _getCurrentTime = getCurrentTime ?? throw new ArgumentNullException(nameof(getCurrentTime));
        _seek = seek ?? throw new ArgumentNullException(nameof(seek));
    }

    public bool IsPlaying { get; private set; }

    public float CurrentTime => _getCurrentTime();

    public void Play()
    {
        IsPlaying = true;
    }

    public void Pause()
    {
        IsPlaying = false;
    }

    public void Stop()
    {
        IsPlaying = false;
        Seek(0f);
    }

    public void Seek(float time)
    {
        _seek(time);
    }

    public void Update(float deltaTime)
    {
        // La preview Animation2D avance dans Animation2dAssetInspectorPanel.Update, pas dans ce
        // controleur : le temps courant est lu via CurrentTime. Aucune avance manuelle ici.
    }
}

namespace CasaEngine.Editor.Controls.Timeline.Playback;

public interface ITimelinePlaybackController
{
    bool IsPlaying { get; }

    float CurrentTime { get; }

    void Play();

    void Pause();

    void Stop();

    void Seek(float time);

    void Update(float deltaTime);
}

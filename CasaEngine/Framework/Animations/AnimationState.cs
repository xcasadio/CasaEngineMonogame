namespace CasaEngine.Framework.Animations;

public sealed class AnimationState
{
    public AnimationState(AnimationClip clip, bool loop, float speed = 1f)
    {
        Clip = clip ?? throw new ArgumentNullException(nameof(clip));
        Loop = loop;
        Speed = speed;
        IsPlaying = true;
    }

    public AnimationClip Clip { get; }

    public float TimeSeconds { get; private set; }

    public float Speed { get; set; } = 1f;

    public bool Loop { get; set; } = true;

    public bool IsPlaying { get; private set; }

    public void Seek(float timeSeconds)
    {
        TimeSeconds = timeSeconds;
    }

    public void Pause()
    {
        IsPlaying = false;
    }

    public void Resume()
    {
        IsPlaying = true;
    }

    public void Stop()
    {
        TimeSeconds = 0f;
        IsPlaying = false;
    }

    public void Update(float elapsedSeconds)
    {
        if (!IsPlaying || elapsedSeconds == 0f)
        {
            return;
        }

        TimeSeconds += elapsedSeconds * Speed;

        if (Loop || Clip.DurationSeconds <= 0f)
        {
            return;
        }

        if (TimeSeconds >= Clip.DurationSeconds)
        {
            TimeSeconds = Clip.DurationSeconds;
            IsPlaying = false;
        }
        else if (TimeSeconds <= 0f)
        {
            TimeSeconds = 0f;
            IsPlaying = false;
        }
    }

    /// <summary>
    /// Advances playback time by <paramref name="elapsedSeconds"/> regardless of the
    /// paused state, without flipping <see cref="IsPlaying"/>. Used for explicit
    /// single-step advancing while the controller is paused.
    /// </summary>
    public void AdvanceForced(float elapsedSeconds)
    {
        if (elapsedSeconds == 0f)
        {
            return;
        }

        TimeSeconds += elapsedSeconds * Speed;

        if (Loop || Clip.DurationSeconds <= 0f)
        {
            return;
        }

        if (TimeSeconds >= Clip.DurationSeconds)
        {
            TimeSeconds = Clip.DurationSeconds;
        }
        else if (TimeSeconds <= 0f)
        {
            TimeSeconds = 0f;
        }
    }
}
using CasaEngine.Core.Time;

namespace CasaEngine.Framework.Gameplay;

public sealed class SurviveTimerObjective : GameplayObjective
{
    public float Duration { get; set; }

    private float _elapsedTime;

    public override void Update(FrameTime frameTime)
    {
        if (IsCompleted || IsFailed)
        {
            return;
        }

        _elapsedTime += frameTime.DeltaTime;

        if (_elapsedTime >= Duration)
        {
            IsCompleted = true;
        }
    }
}
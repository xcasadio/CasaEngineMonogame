using CasaEngine.Core.Time;

namespace CasaEngine.Framework.Gameplay;

public sealed class GameplayModeRunner
{
    public GameplayMode CurrentMode { get; private set; }
    public GameplayState CurrentState { get; private set; }

    public void Start(GameplayMode mode, GameplayContext context)
    {
        ArgumentNullException.ThrowIfNull(mode);
        ArgumentNullException.ThrowIfNull(context);

        Stop();

        CurrentMode = mode;
        CurrentState = new GameplayState();

        mode.Initialize(context, CurrentState);
        mode.Start();

        CurrentState.Phase = GameplayPhase.Playing;
        CurrentState.Result = GameplayResult.Running;
    }

    public void Update(FrameTime frameTime)
    {
        if (CurrentMode == null || CurrentState == null || CurrentState.Phase != GameplayPhase.Playing)
        {
            return;
        }

        CurrentState.ElapsedTime += frameTime.DeltaTime;
        CurrentMode.Update(frameTime);

        GameplayResult result = CurrentMode.EvaluateResult();
        CurrentState.Result = result;

        if (result == GameplayResult.Success)
        {
            CurrentState.Phase = GameplayPhase.Success;
        }
        else if (result == GameplayResult.Failure)
        {
            CurrentState.Phase = GameplayPhase.Failure;
        }
        else if (result == GameplayResult.Cancelled)
        {
            CurrentState.Phase = GameplayPhase.Stopped;
        }
    }

    public void Pause()
    {
        if (CurrentMode == null || CurrentState == null || CurrentState.Phase != GameplayPhase.Playing)
        {
            return;
        }

        CurrentState.Phase = GameplayPhase.Paused;
        CurrentMode.Pause();
    }

    public void Resume()
    {
        if (CurrentMode == null || CurrentState == null || CurrentState.Phase != GameplayPhase.Paused)
        {
            return;
        }

        CurrentState.Phase = GameplayPhase.Playing;
        CurrentMode.Resume();
    }

    public void Stop()
    {
        CurrentMode?.Stop();
        CurrentMode = null;
        CurrentState = null;
    }

    public void Restart()
    {
        if (CurrentMode == null || CurrentState == null)
        {
            return;
        }

        CurrentState.ElapsedTime = 0f;
        CurrentState.Result = GameplayResult.Running;
        CurrentState.Phase = GameplayPhase.Playing;
        CurrentMode.Restart();
    }

    public void Abort()
    {
        if (CurrentMode == null || CurrentState == null)
        {
            return;
        }

        CurrentMode.Abort();
        CurrentState.Result = GameplayResult.Cancelled;
        CurrentState.Phase = GameplayPhase.Stopped;
    }
}
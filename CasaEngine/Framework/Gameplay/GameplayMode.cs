using CasaEngine.Core.Time;

namespace CasaEngine.Framework.Gameplay;

public abstract class GameplayMode
{
    protected GameplayContext Context { get; private set; } = null!;
    protected GameplayState State { get; private set; } = null!;

    public void Initialize(GameplayContext context, GameplayState state)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);

        Context = context;
        State = state;
        OnInitialize();
    }

    protected virtual void OnInitialize()
    {
    }

    public virtual void Start()
    {
    }

    public virtual void Update(FrameTime frameTime)
    {
    }

    public virtual void Pause()
    {
    }

    public virtual void Resume()
    {
    }

    public virtual void Stop()
    {
    }

    public virtual void Restart()
    {
    }

    public virtual void Abort()
    {
    }

    public virtual GameplayResult EvaluateResult()
    {
        return GameplayResult.Running;
    }
}
using CasaEngine.Core.Time;

namespace CasaEngine.Framework.Gameplay;

public abstract class GameplayObjective
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsCompleted { get; protected set; }
    public bool IsFailed { get; protected set; }

    public virtual void Initialize(GameplayContext context)
    {
    }

    public virtual void Update(FrameTime frameTime)
    {
    }
}
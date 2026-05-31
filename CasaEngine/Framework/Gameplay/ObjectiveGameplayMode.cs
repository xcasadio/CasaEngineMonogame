using CasaEngine.Core.Time;

namespace CasaEngine.Framework.Gameplay;

public abstract class ObjectiveGameplayMode : GameplayMode
{
    protected List<GameplayObjective> Objectives { get; } = [];

    public IReadOnlyList<GameplayObjective> ActiveObjectives => Objectives;

    protected override void OnInitialize()
    {
        for (int index = 0; index < Objectives.Count; index++)
        {
            GameplayObjective objective = Objectives[index];
            objective.Initialize(Context);

            if (objective is IGameplayEventListener listener)
            {
                Context.Events.Register(listener);
            }
        }
    }

    public override void Update(FrameTime frameTime)
    {
        for (int index = 0; index < Objectives.Count; index++)
        {
            Objectives[index].Update(frameTime);
        }
    }

    public override GameplayResult EvaluateResult()
    {
        bool hasObjective = false;

        for (int index = 0; index < Objectives.Count; index++)
        {
            GameplayObjective objective = Objectives[index];
            hasObjective = true;

            if (objective.IsFailed)
            {
                return GameplayResult.Failure;
            }

            if (!objective.IsCompleted)
            {
                return GameplayResult.Running;
            }
        }

        return hasObjective ? GameplayResult.Success : GameplayResult.Running;
    }

    public override void Stop()
    {
        for (int index = 0; index < Objectives.Count; index++)
        {
            if (Objectives[index] is IGameplayEventListener listener)
            {
                Context.Events.Unregister(listener);
            }
        }
    }
}
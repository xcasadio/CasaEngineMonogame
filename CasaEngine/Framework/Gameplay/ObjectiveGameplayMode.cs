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
            Objectives[index].Initialize(Context);
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
}
namespace CasaEngine.Framework.Cutscenes;

public sealed class ParallelCutsceneActionData : CutsceneActionData
{
    public override string Type => CutsceneActionTypes.Parallel;

    public List<CutsceneActionData> Actions { get; } = [];
}
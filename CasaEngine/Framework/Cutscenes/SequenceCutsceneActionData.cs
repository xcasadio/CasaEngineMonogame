namespace CasaEngine.Framework.Cutscenes;

public sealed class SequenceCutsceneActionData : CutsceneActionData
{
    public override string Type => CutsceneActionTypes.Sequence;

    public List<CutsceneActionData> Actions { get; } = [];
}
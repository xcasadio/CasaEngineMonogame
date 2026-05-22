namespace CasaEngine.Framework.Cutscenes;

public sealed class WaitCutsceneActionData : CutsceneActionData
{
    public override string Type => CutsceneActionTypes.Wait;

    public float Seconds { get; set; }
}
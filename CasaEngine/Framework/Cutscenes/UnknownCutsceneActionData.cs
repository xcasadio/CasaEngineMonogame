namespace CasaEngine.Framework.Cutscenes;

public sealed class UnknownCutsceneActionData : CutsceneActionData
{
    public UnknownCutsceneActionData(string type)
    {
        UnknownType = type;
    }

    public override string Type => UnknownType;

    public string UnknownType { get; }
}
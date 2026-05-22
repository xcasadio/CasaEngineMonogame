namespace CasaEngine.Framework.Cutscenes;

public sealed class CutsceneValidationResult
{
    private readonly List<CutsceneValidationMessage> _messages = [];

    public IReadOnlyList<CutsceneValidationMessage> Messages => _messages;

    public bool IsValid
    {
        get
        {
            for (int index = 0; index < _messages.Count; index++)
            {
                if (_messages[index].Severity == CutsceneValidationSeverity.Error)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public void AddWarning(string path, string message)
    {
        _messages.Add(new CutsceneValidationMessage(CutsceneValidationSeverity.Warning, path, message));
    }

    public void AddError(string path, string message)
    {
        _messages.Add(new CutsceneValidationMessage(CutsceneValidationSeverity.Error, path, message));
    }
}
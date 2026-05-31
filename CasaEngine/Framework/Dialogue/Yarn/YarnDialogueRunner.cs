using CasaEngine.Framework.Dialogue.Assets;
using CasaEngine.Framework.Dialogue.Presentation;
using CasaEngine.Framework.Dialogue.Runtime;

namespace CasaEngine.Framework.Dialogue.Yarn;

public sealed class YarnDialogueRunner
{
    private readonly IDialoguePresenter _presenter;
    private DialogueAsset _asset;
    private global::Yarn.Dialogue _dialogue;

    public YarnDialogueRunner(IDialoguePresenter presenter)
    {
        ArgumentNullException.ThrowIfNull(presenter);

        _presenter = presenter;
    }

    public bool IsRunning => _dialogue?.IsActive == true;

    public bool Start(DialogueAsset asset)
        => Start(asset, asset?.StartNode);

    public bool Start(DialogueAsset asset, string startNode)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (!asset.HasCompiledProgram)
        {
            return false;
        }

        string nodeName = string.IsNullOrWhiteSpace(startNode) ? "Start" : startNode;
        global::Yarn.Program program = global::Yarn.Program.Parser.ParseFrom(asset.ProgramBytes);
        global::Yarn.Dialogue dialogue = CreateDialogue(program);
        dialogue.SetProgram(program);
        if (!dialogue.NodeExists(nodeName))
        {
            return false;
        }

        Stop();
        _asset = asset;
        _dialogue = dialogue;
        _dialogue.SetNode(nodeName);
        _dialogue.Continue();
        return true;
    }

    public bool Continue()
    {
        if (_dialogue == null || !_dialogue.IsActive)
        {
            return false;
        }

        _dialogue.Continue();
        return true;
    }

    public bool Stop()
    {
        bool wasRunning = _dialogue != null || _presenter.IsOpen;
        _dialogue?.Stop();
        _dialogue = null;
        _asset = null;
        _presenter.Close();
        return wasRunning;
    }

    private global::Yarn.Dialogue CreateDialogue(global::Yarn.Program program)
    {
        var variableStore = new global::Yarn.MemoryVariableStore
        {
            Program = program,
        };

        return new global::Yarn.Dialogue(variableStore)
        {
            LineHandler = OnLine,
            OptionsHandler = OnOptions,
            CommandHandler = OnCommand,
            DialogueCompleteHandler = OnDialogueComplete,
        };
    }

    private void OnLine(global::Yarn.Line line)
    {
        string text = ResolveLineText(line);
        _presenter.ShowLine(new DialogueLine(text));
    }

    private void OnOptions(global::Yarn.OptionSet options)
    {
    }

    private void OnCommand(global::Yarn.Command command)
    {
    }

    private void OnDialogueComplete()
    {
        _dialogue = null;
        _asset = null;
        _presenter.Close();
    }

    private string ResolveLineText(global::Yarn.Line line)
    {
        if (_asset == null || !_asset.LineTexts.TryGetValue(line.ID, out string text))
        {
            text = line.ID;
        }

        string[] substitutions = line.Substitutions;
        for (int index = 0; index < substitutions.Length; index++)
        {
            text = text.Replace("{" + index + "}", substitutions[index] ?? string.Empty, StringComparison.Ordinal);
        }

        return text;
    }
}
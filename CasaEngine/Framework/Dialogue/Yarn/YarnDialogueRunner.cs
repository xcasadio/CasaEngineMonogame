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

    /// <summary>
    /// The variable storage bound to every <see cref="global::Yarn.Dialogue"/> this runner creates.
    /// When <see langword="null"/> (the default), each <see cref="Start(DialogueAsset)"/> creates a
    /// fresh <see cref="global::Yarn.MemoryVariableStore"/>, as before this property existed.
    /// Set it once and keep it across calls to <see cref="Start(DialogueAsset)"/> so a game's Yarn
    /// variables persist from one dialogue to the next.
    /// </summary>
    public global::Yarn.IVariableStorage VariableStorage { get; set; }

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
        if (program.Nodes == null || !program.Nodes.ContainsKey(nodeName))
        {
            // The requested node does not exist on this program: fail before touching the dialogue
            // currently running, or the (possibly shared) variable storage.
            return false;
        }

        global::Yarn.Dialogue dialogue = CreateDialogue(program);
        dialogue.SetProgram(program);

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
        global::Yarn.IVariableStorage variableStorage = VariableStorage ?? new global::Yarn.MemoryVariableStore
        {
            Program = program,
        };

        return new global::Yarn.Dialogue(variableStorage)
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
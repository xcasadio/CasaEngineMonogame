using CasaEngine.Framework.Dialogue.Assets;
using CasaEngine.Framework.Dialogue.Presentation;
using CasaEngine.Framework.Dialogue.Runtime;

namespace CasaEngine.Framework.Dialogue.Yarn;

public sealed class YarnDialogueRunner
{
    private readonly IDialoguePresenter _presenter;
    private readonly Dictionary<string, Action<IReadOnlyList<string>>> _commandHandlers = new(StringComparer.Ordinal);
    private readonly HashSet<string> _warnedUnhandledCommandNames = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Delegate> _functions = new(StringComparer.Ordinal);
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

    /// <summary>
    /// Raised when a <c>&lt;&lt;command&gt;&gt;</c> line names a command with no handler registered
    /// through <see cref="AddCommandHandler"/>. The dialogue resumes right after this event fires;
    /// it never blocks on an unknown command.
    /// </summary>
    public event EventHandler<UnhandledYarnCommandEventArgs> UnhandledCommand;

    /// <summary>
    /// Registers a handler for the named Yarn <c>&lt;&lt;command&gt;&gt;</c>. The dialogue that raised
    /// the command resumes automatically once <paramref name="handler"/> returns, unless it called
    /// <see cref="Stop"/> or started a different dialogue.
    /// </summary>
    /// <exception cref="ArgumentException">A handler is already registered for <paramref name="name"/>.</exception>
    public void AddCommandHandler(string name, Action<IReadOnlyList<string>> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(handler);

        _commandHandlers.Add(name, handler);
    }

    /// <summary>Removes the handler registered for <paramref name="name"/>, if any.</summary>
    /// <returns><see langword="true"/> if a handler was removed.</returns>
    public bool RemoveCommandHandler(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return _commandHandlers.Remove(name);
    }

    /// <summary>
    /// Registers <paramref name="implementation"/> as the Yarn function <paramref name="name"/>
    /// (called from a line as <c>{name(args)}</c> or from a condition as <c>&lt;&lt;if name(args)&gt;&gt;</c>).
    /// It is applied to the <see cref="global::Yarn.Library"/> of every <see cref="global::Yarn.Dialogue"/>
    /// this runner creates afterwards, including across calls to <see cref="Start(DialogueAsset)"/>.
    /// For the call to compile, the same name and delegate shape must have been declared to
    /// <c>YarnDialogueCompiler</c> when the script was compiled.
    /// </summary>
    /// <exception cref="ArgumentException">A function is already registered for <paramref name="name"/>.</exception>
    public void RegisterFunction(string name, Delegate implementation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(implementation);

        _functions.Add(name, implementation);
    }

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

        var dialogue = new global::Yarn.Dialogue(variableStorage)
        {
            LineHandler = OnLine,
            OptionsHandler = OnOptions,
            CommandHandler = OnCommand,
            DialogueCompleteHandler = OnDialogueComplete,
        };

        foreach (KeyValuePair<string, Delegate> function in _functions)
        {
            dialogue.Library.RegisterFunction(function.Key, function.Value);
        }

        return dialogue;
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
        global::Yarn.Dialogue commandDialogue = _dialogue;
        List<string> tokens = YarnCommandLine.Tokenize(command.Text);
        string name = tokens.Count > 0 ? tokens[0] : string.Empty;
        List<string> arguments = tokens.Count > 1 ? tokens.GetRange(1, tokens.Count - 1) : new List<string>();

        if (name.Length > 0 && _commandHandlers.TryGetValue(name, out Action<IReadOnlyList<string>> handler))
        {
            try
            {
                handler(arguments);
            }
            catch
            {
                Stop();
                throw;
            }
        }
        else
        {
            if (_warnedUnhandledCommandNames.Add(name))
            {
                System.Diagnostics.Debug.WriteLine(
                    name.Length > 0
                        ? $"YarnDialogueRunner: no handler registered for command '{name}'."
                        : "YarnDialogueRunner: a command line had empty text.");
            }

            UnhandledCommand?.Invoke(this, new UnhandledYarnCommandEventArgs(name, arguments));
        }

        if (ReferenceEquals(_dialogue, commandDialogue) && commandDialogue != null && commandDialogue.IsActive)
        {
            // The command completed synchronously and neither stopped this dialogue nor started
            // another one: resume immediately. Yarn Spinner is still inside the Continue() call
            // that dispatched this command (Dialogue.Continue() is reentrancy-guarded and would be
            // a no-op here), so SignalContentComplete is the documented way to resume synchronously.
            commandDialogue.SignalContentComplete();
        }
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
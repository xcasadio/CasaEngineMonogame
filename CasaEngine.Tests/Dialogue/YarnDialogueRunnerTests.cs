using CasaEngine.Compiler.Dialogue;
using CasaEngine.Framework.Dialogue.Assets;
using CasaEngine.Framework.Dialogue.Presentation;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.Dialogue.Yarn;
using Xunit;

namespace CasaEngine.Tests.Dialogue;

public sealed class YarnDialogueRunnerTests
{
    [Fact]
    public void Start_ShowsFirstYarnLine()
    {
        DialogueAsset asset = CreateGreetingAsset();
        var presenter = new FakeDialoguePresenter();
        var runner = new YarnDialogueRunner(presenter);

        bool started = runner.Start(asset);

        Assert.True(started);
        Assert.True(runner.IsRunning);
        Assert.True(presenter.IsOpen);
        Assert.Equal("Bonjour depuis CasaEngine.", presenter.CurrentLine.Text);
    }

    [Fact]
    public void Continue_ClosesPresenterWhenDialogueCompletes()
    {
        DialogueAsset asset = CreateGreetingAsset();
        var presenter = new FakeDialoguePresenter();
        var runner = new YarnDialogueRunner(presenter);
        runner.Start(asset);

        bool continued = runner.Continue();

        Assert.True(continued);
        Assert.False(runner.IsRunning);
        Assert.False(presenter.IsOpen);
        Assert.True(presenter.CurrentLine.IsEmpty);
    }

    [Fact]
    public void Start_InjectedVariableStorage_SetAffectsSubsequentIf()
    {
        DialogueAsset asset = CompileAsset(
            "Conditional",
            """
            title: Start
            ---
            <<set $x to true>>
            <<if $x>>
            Yes.
            <<else>>
            No.
            <<endif>>
            ===
            """);
        var storage = new global::Yarn.MemoryVariableStore();
        var presenter = new FakeDialoguePresenter();
        var runner = new YarnDialogueRunner(presenter) { VariableStorage = storage };

        bool started = runner.Start(asset);

        Assert.True(started);
        Assert.Equal("Yes.", presenter.CurrentLine.Text);
    }

    [Fact]
    public void Start_InjectedVariableStorage_PersistsAcrossStartCalls()
    {
        DialogueAsset setAsset = CompileAsset(
            "Setter",
            """
            title: Start
            ---
            <<set $x to true>>
            Set.
            ===
            """);
        DialogueAsset checkAsset = CompileAsset(
            "Checker",
            """
            title: Start
            ---
            <<if $x>>
            Yes.
            <<else>>
            No.
            <<endif>>
            ===
            """);
        var storage = new global::Yarn.MemoryVariableStore();
        var presenter = new FakeDialoguePresenter();
        var runner = new YarnDialogueRunner(presenter) { VariableStorage = storage };

        runner.Start(setAsset);
        runner.Start(checkAsset);

        Assert.Equal("Yes.", presenter.CurrentLine.Text);
    }

    [Fact]
    public void Start_RefusedOnUnknownNode_LeavesCurrentDialogueRunningWithSharedStorage()
    {
        DialogueAsset assetA = CompileAsset(
            "AssetA",
            """
            title: Start
            ---
            <<set $x to true>>
            First from A.
            Second from A.
            ===
            """);
        DialogueAsset assetB = CompileAsset(
            "AssetB",
            """
            title: Start
            ---
            From B.
            ===
            """);
        var storage = new global::Yarn.MemoryVariableStore();
        var presenter = new FakeDialoguePresenter();
        var runner = new YarnDialogueRunner(presenter) { VariableStorage = storage };
        runner.Start(assetA);

        bool started = runner.Start(assetB, "Absent");

        Assert.False(started);
        Assert.True(runner.IsRunning);
        Assert.Equal("First from A.", presenter.CurrentLine.Text);

        runner.Continue();

        Assert.Equal("Second from A.", presenter.CurrentLine.Text);
        Assert.True(storage.TryGetValue<bool>("$x", out bool x));
        Assert.True(x);
    }

    [Fact]
    public void Start_WithoutInjectedVariableStorage_BehavesAsBefore()
    {
        DialogueAsset asset = CreateGreetingAsset();
        var presenter = new FakeDialoguePresenter();
        var runner = new YarnDialogueRunner(presenter);

        bool started = runner.Start(asset);

        Assert.True(started);
        Assert.Null(runner.VariableStorage);
        Assert.Equal("Bonjour depuis CasaEngine.", presenter.CurrentLine.Text);
    }

    [Fact]
    public void Start_InjectedMemoryVariableStore_KeepsVisitedFunctional()
    {
        DialogueAsset asset = CompileAsset(
            "Visited",
            """
            title: Start
            ---
            <<if visited("Start")>>
            Back again.
            <<else>>
            First time.
            <<endif>>
            <<jump Start>>
            ===
            """);
        var storage = new global::Yarn.MemoryVariableStore();
        var presenter = new FakeDialoguePresenter();
        var runner = new YarnDialogueRunner(presenter) { VariableStorage = storage };

        runner.Start(asset);
        Assert.Equal("First time.", presenter.CurrentLine.Text);

        runner.Continue();
        Assert.Equal("Back again.", presenter.CurrentLine.Text);
    }

    [Fact]
    public void Command_RegisteredHandler_ReceivesArgumentsAndDialogueResumes()
    {
        DialogueAsset asset = CompileAsset(
            "Command",
            """
            title: Start
            ---
            <<set $n to 3>>
            <<greet "Alice" {$n}>>
            After command.
            ===
            """);
        var presenter = new FakeDialoguePresenter();
        var runner = new YarnDialogueRunner(presenter);
        IReadOnlyList<string> receivedArguments = null;
        runner.AddCommandHandler("greet", args => receivedArguments = args);

        runner.Start(asset);

        Assert.NotNull(receivedArguments);
        Assert.Equal(new[] { "Alice", "3" }, receivedArguments);
        Assert.Equal("After command.", presenter.CurrentLine.Text);
    }

    [Fact]
    public void Command_Unknown_RaisesEventAndDoesNotBlock()
    {
        DialogueAsset asset = CompileAsset(
            "UnknownCommand",
            """
            title: Start
            ---
            <<mystery arg>>
            After unknown.
            ===
            """);
        var presenter = new FakeDialoguePresenter();
        var runner = new YarnDialogueRunner(presenter);
        var unhandled = new List<string>();
        runner.UnhandledCommand += (_, e) => unhandled.Add(e.Name);

        runner.Start(asset);

        Assert.Equal(new[] { "mystery" }, unhandled);
        Assert.Equal("After unknown.", presenter.CurrentLine.Text);
    }

    [Fact]
    public void Command_HandlerCallsStop_ClosesPresenterWithoutThrowing()
    {
        DialogueAsset asset = CompileAsset(
            "StoppingCommand",
            """
            title: Start
            ---
            First.
            <<stopnow>>
            Never shown.
            ===
            """);
        var presenter = new FakeDialoguePresenter();
        var runner = new YarnDialogueRunner(presenter);
        runner.AddCommandHandler("stopnow", _ => runner.Stop());
        runner.Start(asset);

        runner.Continue();

        Assert.False(runner.IsRunning);
        Assert.False(presenter.IsOpen);
    }

    [Fact]
    public void Command_HandlerStartsAnotherDialogue_ShowsItsFirstLineWithoutSkipping()
    {
        DialogueAsset other = CompileAsset(
            "OtherDialogue",
            """
            title: Start
            ---
            From the other dialogue.
            ===
            """);
        DialogueAsset asset = CompileAsset(
            "SwitchingCommand",
            """
            title: Start
            ---
            First.
            <<switch>>
            Never shown.
            ===
            """);
        var presenter = new FakeDialoguePresenter();
        var runner = new YarnDialogueRunner(presenter);
        runner.AddCommandHandler("switch", _ => runner.Start(other));
        runner.Start(asset);

        runner.Continue();

        Assert.True(runner.IsRunning);
        Assert.Equal("From the other dialogue.", presenter.CurrentLine.Text);
    }

    [Fact]
    public void Command_HandlerThrows_ExceptionPropagatesAndRunnerStops()
    {
        DialogueAsset asset = CompileAsset(
            "ThrowingCommand",
            """
            title: Start
            ---
            First.
            <<explode>>
            Never shown.
            ===
            """);
        var presenter = new FakeDialoguePresenter();
        var runner = new YarnDialogueRunner(presenter);
        runner.AddCommandHandler("explode", _ => throw new InvalidOperationException("boom"));
        runner.Start(asset);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => runner.Continue());

        Assert.Equal("boom", exception.Message);
        Assert.False(runner.IsRunning);
        Assert.False(presenter.IsOpen);
    }

    [Fact]
    public void AddCommandHandler_DuplicateName_ThrowsArgumentException()
    {
        var presenter = new FakeDialoguePresenter();
        var runner = new YarnDialogueRunner(presenter);
        runner.AddCommandHandler("greet", _ => { });

        Assert.Throws<ArgumentException>(() => runner.AddCommandHandler("greet", _ => { }));
    }

    private static DialogueAsset CreateGreetingAsset()
    {
        string sourceFileName = Path.Combine(FindRepositoryRoot(), "CasaEngine.Tests", "Dialogue", "Fixtures", "greeting.yarn");
        var compiler = new YarnDialogueCompiler();
        YarnDialogueCompilationResult result = compiler.CompileFile(sourceFileName);
        if (!result.Success)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        return DialogueAsset.FromCompiledProgram("Greeting", "Start", result.ProgramBytes, result.LineTexts);
    }

    private static DialogueAsset CompileAsset(string name, string source)
    {
        var compiler = new YarnDialogueCompiler();
        YarnDialogueCompilationResult result = compiler.CompileString(source, name + ".yarn");
        if (!result.Success)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        return DialogueAsset.FromCompiledProgram(name, "Start", result.ProgramBytes, result.LineTexts);
    }

    private static string FindRepositoryRoot()
    {
        string directory = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(directory, "CasaEngine.MonoGame.sln")))
        {
            DirectoryInfo parent = Directory.GetParent(directory);
            if (parent == null)
            {
                throw new InvalidOperationException("Cannot find repository root.");
            }

            directory = parent.FullName;
        }

        return directory;
    }

    private sealed class FakeDialoguePresenter : IDialoguePresenter
    {
        public DialogueRuntimeState State { get; private set; } = DialogueRuntimeState.Closed;
        public DialogueLine CurrentLine { get; private set; } = DialogueLine.Empty;
        public bool IsOpen => State == DialogueRuntimeState.Open;

        public IReadOnlyList<string> Choices => Array.Empty<string>();
        public bool HasChoices => false;

        public event EventHandler<DialoguePresentationChangedEventArgs> PresentationChanged;
        public event EventHandler<DialogueChoiceSelectedEventArgs> ChoiceSelected;

        public bool ShowLine(DialogueLine line)
        {
            ArgumentNullException.ThrowIfNull(line);

            DialogueRuntimeState previousState = State;
            State = DialogueRuntimeState.Open;
            CurrentLine = line;
            PresentationChanged?.Invoke(this, new DialoguePresentationChangedEventArgs(previousState, State, CurrentLine));
            return true;
        }

        public bool ShowChoices(IReadOnlyList<string> labels) => throw new NotSupportedException("Not used by YarnDialogueRunnerTests.");

        public bool SelectChoice(int index) => throw new NotSupportedException("Not used by YarnDialogueRunnerTests.");

        public bool Close()
        {
            if (!IsOpen)
            {
                return false;
            }

            DialogueRuntimeState previousState = State;
            State = DialogueRuntimeState.Closed;
            CurrentLine = DialogueLine.Empty;
            PresentationChanged?.Invoke(this, new DialoguePresentationChangedEventArgs(previousState, State, CurrentLine));
            return true;
        }
    }
}
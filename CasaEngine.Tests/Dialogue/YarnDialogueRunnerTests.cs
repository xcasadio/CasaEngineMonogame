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
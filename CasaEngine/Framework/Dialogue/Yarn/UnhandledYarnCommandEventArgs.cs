namespace CasaEngine.Framework.Dialogue.Yarn;

/// <summary>
/// Raised by <see cref="YarnDialogueRunner.UnhandledCommand"/> when a <c>&lt;&lt;command&gt;&gt;</c>
/// line names a command with no registered handler.
/// </summary>
public sealed class UnhandledYarnCommandEventArgs : EventArgs
{
    public UnhandledYarnCommandEventArgs(string name, IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(arguments);

        Name = name;
        Arguments = arguments;
    }

    /// <summary>The command name, or an empty string when the command's text itself was empty.</summary>
    public string Name { get; }
    public IReadOnlyList<string> Arguments { get; }
}

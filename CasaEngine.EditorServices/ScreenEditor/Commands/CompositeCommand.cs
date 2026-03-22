using System.Collections.Generic;
using System.Linq;

namespace CasaEngine.EditorServices.ScreenEditor.Commands;

/// <summary>
/// Groups multiple <see cref="IUIScreenCommand"/> instances into a single undoable/redoable operation.
/// <para/>
/// All sub-commands are executed in order; undone in reverse order.
/// </summary>
public sealed class CompositeCommand : IUIScreenCommand
{
    private readonly IUIScreenCommand[] _commands;

    public string Description { get; }

    public CompositeCommand(string description, params IUIScreenCommand[] commands)
    {
        Description = description;
        _commands = (IUIScreenCommand[])commands.Clone();
    }

    public CompositeCommand(string description, IEnumerable<IUIScreenCommand> commands)
    {
        Description = description;
        _commands = commands.ToArray();
    }

    public void Execute()
    {
        foreach (var command in _commands)
        {
            command.Execute();
        }
    }

    public void Undo()
    {
        for (var i = _commands.Length - 1; i >= 0; i--)
        {
            _commands[i].Undo();
        }
    }
}

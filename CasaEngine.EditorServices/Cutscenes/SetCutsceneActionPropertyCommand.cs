using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Cutscenes;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>
/// Sets one property of a cutscene action. It keeps the old and the new typed values, so that undoing puts the old value
/// back without parsing a text, and it clears the format error and the load loss of the field, which undoing restores.
/// </summary>
public sealed class SetCutsceneActionPropertyCommand : IEditorCommand
{
    private readonly CutsceneEditSession _session;
    private readonly CutsceneActionData _action;
    private readonly CutsceneActionProperty _property;
    private readonly object? _newValue;
    private readonly object? _oldValue;
    private readonly string? _oldFormatError;
    private readonly CutsceneLoadLoss? _oldLoadLoss;

    /// <param name="newValue">The typed value, as <see cref="CutsceneActionProperty.TryParse"/> gives it.</param>
    /// <exception cref="ArgumentException">The property is not one of the action, or the value has the wrong type.</exception>
    public SetCutsceneActionPropertyCommand(CutsceneEditSession session, CutsceneActionData action, CutsceneActionProperty property, object? newValue)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(property);

        if (!CutsceneActionPropertyEditor.GetProperties(action).Contains(property))
        {
            throw new ArgumentException($"'{property.Key}' is not an editable property of a {action.Type} action.", nameof(property));
        }

        property.ValidateValue(newValue);

        _session = session;
        _action = action;
        _property = property;
        _oldValue = property.GetValue(action);
        _newValue = CopyIfList(newValue);

        var formatErrors = CutsceneActionPropertyEditor.GetFormatErrors(action);
        if (formatErrors != null && formatErrors.TryGetValue(property.Key, out string? formatError))
        {
            _oldFormatError = formatError;
        }

        _oldLoadLoss = session.FindLoadLoss(action, property.Key);
        Description = $"Set {action.Type}.{property.Key}";
    }

    public string Description { get; }

    public void Execute()
    {
        _property.SetValue(_action, _newValue);
        CutsceneActionPropertyEditor.GetFormatErrors(_action)?.Remove(_property.Key);
        if (_oldLoadLoss != null)
        {
            _session.RemoveLoadLoss(_oldLoadLoss);
        }
    }

    public void Undo()
    {
        _property.SetValue(_action, _oldValue);
        if (_oldFormatError != null)
        {
            CutsceneActionPropertyEditor.GetFormatErrors(_action)![_property.Key] = _oldFormatError;
        }

        if (_oldLoadLoss != null && _session.FindLoadLoss(_action, _property.Key) == null)
        {
            _session.AddLoadLoss(_oldLoadLoss);
        }
    }

    private object? CopyIfList(object? value)
        => _property.Kind == CutsceneActionPropertyKind.StringList && value is IEnumerable<string> list
            ? new List<string>(list)
            : value;
}

using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.EditorServices.ScreenEditor.Factory;
using CasaEngine.EditorServices.ScreenEditor.Toolbox;

namespace CasaEngine.EditorServices.ScreenEditor.Commands;

/// <summary>
/// Reversible command that creates a <see cref="UIScreenNode"/> from a
/// <see cref="UIControlRegistryEntry"/> and inserts it into the document.
/// </summary>
public sealed class AddNodeCommand : IUIScreenCommand
{
    private readonly UIScreenDocument _document;
    private readonly UIControlRegistryEntry _entry;
    private readonly UIScreenNode? _requestedParent;

    // Filled in during Execute to support Undo
    private UIScreenNode? _addedNode;
    private UIScreenNode? _actualParent;
    // For the "wrap root" fallback: original root before wrapping
    private UIScreenNode? _originalRoot;
    private bool _rootWasWrapped;

    public string Description => $"Add {_entry.DisplayName}";

    public AddNodeCommand(
        UIScreenDocument document,
        UIControlRegistryEntry entry,
        UIScreenNode? requestedParent)
    {
        _document        = document;
        _entry           = entry;
        _requestedParent = requestedParent;
    }

    public void Execute()
    {
        // Snapshot state before insertion for Undo
        _originalRoot   = _document.Root;
        _rootWasWrapped = false;

        _addedNode    = UIScreenNodeFactory.Create(_document, _entry, _requestedParent);
        _actualParent = _addedNode.Parent;

        // Detect if WrapRootAndInsert was triggered (a new StackPanel wrapper appeared as root)
        if (!ReferenceEquals(_document.Root, _originalRoot))
        {
            _rootWasWrapped = true;
        }
    }

    public void Undo()
    {
        if (_addedNode == null)
        {
            return;
        }

        if (_rootWasWrapped && _originalRoot != null)
        {
            // Restore the original root by unwrapping
            _document.SetRoot(_originalRoot);
        }
        else if (_actualParent != null)
        {
            _actualParent.RemoveChild(_addedNode);
        }
        else
        {
            // Node was set as root (empty document case)
            _document.ClearRoot();
        }

        _addedNode = null;
    }
}

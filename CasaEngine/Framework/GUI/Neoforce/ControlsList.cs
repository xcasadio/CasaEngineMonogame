namespace CasaEngine.Framework.GUI.Neoforce;

/// <summary>
/// Defines type used as a controls collection.
/// </summary>
public class ControlsList : EventedList<Control>
{
    public ControlsList() : base() { }
    public ControlsList(int capacity) : base(capacity) { }
    public ControlsList(IEnumerable<Control> collection) : base(collection) { }
}
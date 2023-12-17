using System.Collections.Generic;

namespace TomShane.Neoforce.Controls;

/// <summary>
/// Defines type used as a controls collection.
/// </summary>
public class ControlsList : EventedList<Control>
{
    public ControlsList() : base() { }
    public ControlsList(int capacity) : base(capacity) { }
    public ControlsList(IEnumerable<Control> collection) : base(collection) { }
}
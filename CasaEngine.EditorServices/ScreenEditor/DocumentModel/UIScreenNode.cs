namespace CasaEngine.EditorServices.ScreenEditor.DocumentModel;

public sealed class UIScreenNode
{
    private readonly List<UIScreenNode> _children = new();
    private readonly Dictionary<string, UIScreenPropertyValue> _properties = new(StringComparer.Ordinal);

    public UIScreenNode(string controlType)
    {
        if (string.IsNullOrWhiteSpace(controlType))
        {
            throw new ArgumentException("Control type cannot be null or whitespace.", nameof(controlType));
        }

        ControlType = controlType;
    }

    public DocumentNodeId Id { get; } = DocumentNodeId.NewId();

    public string ControlType { get; }

    public string? Name { get; set; }

    public UIScreenDesignFlags DesignFlags { get; set; }

    public UIScreenNode? Parent { get; private set; }

    public IReadOnlyList<UIScreenNode> Children => _children;

    public IReadOnlyDictionary<string, UIScreenPropertyValue> Properties => _properties;

    public IDictionary<string, object?> TransientAnnotations { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    public void AddChild(UIScreenNode child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (ReferenceEquals(child, this))
        {
            throw new InvalidOperationException("A node cannot be added as its own child.");
        }

        if (IsAncestorOf(child))
        {
            throw new InvalidOperationException("A node cannot be reparented under one of its descendants.");
        }

        child.DetachFromParent();
        child.Parent = this;
        _children.Add(child);
    }

    public bool RemoveChild(UIScreenNode child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (!_children.Remove(child))
        {
            return false;
        }

        child.Parent = null;
        return true;
    }

    public UIScreenPropertyValue SetProperty(string name, string? serializedValue, string valueType = "string")
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Property name cannot be null or whitespace.", nameof(name));
        }

        if (_properties.TryGetValue(name, out var propertyValue))
        {
            propertyValue.SetValue(serializedValue, valueType);
            return propertyValue;
        }

        propertyValue = new UIScreenPropertyValue(name, serializedValue, valueType);
        _properties.Add(name, propertyValue);
        return propertyValue;
    }

    public bool RemoveProperty(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return _properties.Remove(name);
    }

    public bool TryGetProperty(string name, out UIScreenPropertyValue? propertyValue)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            propertyValue = null;
            return false;
        }

        return _properties.TryGetValue(name, out propertyValue);
    }

    internal void DetachFromParent()
    {
        Parent?.RemoveChild(this);
    }

    private bool IsAncestorOf(UIScreenNode candidate)
    {
        for (var current = Parent; current != null; current = current.Parent)
        {
            if (ReferenceEquals(current, candidate))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Creates a deep clone of this node and its entire subtree.
    /// The clone receives new <see cref="DocumentNodeId"/> values.
    /// </summary>
    public UIScreenNode DeepClone()
    {
        var clone = new UIScreenNode(ControlType)
        {
            Name = Name,
            DesignFlags = DesignFlags,
        };

        foreach (var prop in Properties.Values)
        {
            clone.SetProperty(prop.Name, prop.SerializedValue, prop.ValueType);
        }

        foreach (var child in Children)
        {
            clone.AddChild(child.DeepClone());
        }

        return clone;
    }
}
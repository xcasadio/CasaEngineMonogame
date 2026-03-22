using System;
using System.Collections.Generic;

namespace CasaEngine.Editor.Workspaces;

public sealed class EditorPanelRegistry
{
    private readonly Dictionary<string, EditorPanelDescriptor> _descriptors;

    public EditorPanelRegistry(IEnumerable<EditorPanelDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        _descriptors = new Dictionary<string, EditorPanelDescriptor>(StringComparer.Ordinal);

        foreach (var descriptor in descriptors)
        {
            _descriptors[descriptor.Id] = descriptor;
        }
    }

    public IReadOnlyCollection<EditorPanelDescriptor> Descriptors => _descriptors.Values;

    public bool TryGetDescriptor(string panelId, out EditorPanelDescriptor descriptor)
        => _descriptors.TryGetValue(panelId, out descriptor!);

    public EditorPanelDescriptor GetDescriptor(string panelId)
    {
        if (!_descriptors.TryGetValue(panelId, out var descriptor))
        {
            throw new KeyNotFoundException($"Editor panel descriptor not found: {panelId}");
        }

        return descriptor;
    }
}
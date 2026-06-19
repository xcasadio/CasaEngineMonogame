using System;
using System.Collections.Generic;

namespace CasaEngine.Editor.Controls.ContextualPanels;

/// <summary>
/// Maps editor document kinds to the specialized tool panels that are only relevant
/// while a document of that kind is the active document.
/// This type is pure policy/data: it holds no UI state and performs no dock manipulation.
/// The orchestration (adding/removing dock panels) is performed by the editor shell.
/// </summary>
public sealed class ContextualToolPanelRegistry
{
    private readonly Dictionary<EditorDocumentKind, List<string>> _panelIdsByKind = new();
    private readonly HashSet<string> _managedPanelIds = new(StringComparer.Ordinal);

    /// <summary>
    /// Declares that <paramref name="panelId"/> is a specialized tool panel that is only
    /// relevant while a document of <paramref name="documentKind"/> is active.
    /// A panel may be registered for several document kinds.
    /// </summary>
    public void Register(EditorDocumentKind documentKind, string panelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(panelId);

        if (!_panelIdsByKind.TryGetValue(documentKind, out var panelIds))
        {
            panelIds = new List<string>();
            _panelIdsByKind.Add(documentKind, panelIds);
        }

        if (!panelIds.Contains(panelId))
        {
            panelIds.Add(panelId);
        }

        _managedPanelIds.Add(panelId);
    }

    /// <summary>All tool panel ids managed by this registry, across every document kind.</summary>
    public IReadOnlyCollection<string> ManagedPanelIds => _managedPanelIds;

    /// <summary>
    /// Returns true when <paramref name="panelId"/> is relevant for the supplied
    /// <paramref name="documentKind"/> (i.e. it should be visible in the dock).
    /// </summary>
    public bool IsPanelRelevant(EditorDocumentKind documentKind, string panelId)
    {
        return _panelIdsByKind.TryGetValue(documentKind, out var panelIds)
               && panelIds.Contains(panelId);
    }
}

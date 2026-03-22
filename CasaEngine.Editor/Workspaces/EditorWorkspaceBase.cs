using System.Collections.Generic;
using System.Linq;
using MGUI.Core.UI.Docking.DockLayout;

namespace CasaEngine.Editor.Workspaces;

public abstract class EditorWorkspaceBase : IEditorWorkspace
{
    private readonly IReadOnlyList<EditorPanelDescriptor> _panels;

    protected EditorWorkspaceBase(EditorPanelRegistry panelRegistry)
    {
        PanelRegistry = panelRegistry;
        _panels = panelRegistry.Descriptors.ToList();
    }

    protected EditorPanelRegistry PanelRegistry { get; }

    protected abstract EditorPanelScope WorkspaceScope { get; }

    public abstract EditorWorkspaceId Id { get; }

    public abstract string DisplayName { get; }

    public IReadOnlyList<EditorPanelDescriptor> Panels
        => _panels.Where(descriptor => descriptor.Scope == EditorPanelScope.Common || descriptor.Scope == WorkspaceScope).ToList();

    public abstract DockNode CreateDefaultLayout();

    public bool SupportsPanel(string panelId)
        => Panels.Any(descriptor => descriptor.Id == panelId);

    protected DockPanelNode CreatePanelNode(string panelId)
    {
        var descriptor = PanelRegistry.GetDescriptor(panelId);
        return new DockPanelNode(descriptor.Id)
        {
            Title = descriptor.Title,
            DockableType = descriptor.Kind == EditorPanelKind.Document ? DockableType.Document : DockableType.Tool,
            CanClose = descriptor.CanClose,
            CanFloat = descriptor.CanFloat,
            CanAutoHide = descriptor.CanAutoHide,
            ContentFactory = descriptor.ContentFactory,
        };
    }
}